using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Features.Handshakes.Common;
using Hyperledger.Aries.Features.Handshakes.Connection;
using Hyperledger.Aries.Features.Handshakes.Connection.Models;
using Hyperledger.Aries.Payments;
using Hyperledger.Aries.Routing;
using Hyperledger.Aries.Routing.Mediator.Handlers;
using Hyperledger.Aries.Routing.Mediator.Storage;
using Hyperledger.Aries.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;
using static Hyperledger.TestHarness.Mock.InProcAgentV1;
using static Hyperledger.TestHarness.Mock.InProcAgentV2;

namespace Hyperledger.TestHarness.Mock
{
    public class InProcMediatorAgentV1 : InProcAgentV1
    {
        public InProcMediatorAgentV1(IHost host) : base(host)
        {
        }

        protected override void ConfigureHandlers()
        {
            AddConnectionHandler();
            AddHandler<MediatorForwardHandler>();
            AddHandler<RoutingInboxHandler>();
            AddHandler<DefaultStoreBackupHandler>();
            AddHandler<RetrieveBackupHandler>();
        }

        internal Task<AgentPublicConfiguration> HandleDiscoveryAsync() =>
            Host.Services.GetRequiredService<MediatorDiscoveryMiddleware>().GetConfigurationAsync();
    }

    public class InProcMediatorAgentV2 : InProcAgentV2
    {
        public InProcMediatorAgentV2(IHost host) : base(host)
        {
        }

        protected override void ConfigureHandlers()
        {
            AddConnectionHandler();
            AddHandler<MediatorForwardHandler>();
            AddHandler<RoutingInboxHandler>();
            AddHandler<DefaultStoreBackupHandler>();
            AddHandler<RetrieveBackupHandler>();
        }

        internal Task<AgentPublicConfiguration> HandleDiscoveryAsync() =>
            Host.Services.GetRequiredService<MediatorDiscoveryMiddleware>().GetConfigurationAsync();
    }

    public class InProcAgentV1 : AgentBase, IAsyncLifetime
    {
        /// <inheritdoc />
        public InProcAgentV1(IHost host) 
            : base(host.Services.GetService<IServiceProvider>())
        {
            Host = host;
        }

        public IHost Host { get; }
        public IAgentContext Context { get; private set; }
        public IWalletRecordService Records => Host.Services.GetService<IWalletRecordService>();
        public IConnectionService Connections => Host.Services.GetService<IConnectionService>();
        public IMessageService Messages => Host.Services.GetService<IMessageService>();
        public IPaymentService Payments => Host.Services.GetService<IPaymentService>();

        internal Task<MessageContext> HandleAsync(byte[] data) => 
            ProcessAsync(Context, new PackedMessageContext(data));

        /// <inheritdoc />
        protected override void ConfigureHandlers()
        {
            AddConnectionHandler();
            AddCredentialHandler();
            AddDidExchangeHandler();
            AddDiscoveryHandler();
            AddDiscoveryHandler();
            AddForwardHandler();
            AddProofHandler();
            AddRevocationNotificationHandler();
            AddBasicMessageHandler();
            AddHandler<RetrieveBackupHandler>();
            AddHandler<DefaultStoreBackupHandler>();
        }

        #region Factory methods

        public static async Task<PairedAgents> CreatePairedAsync(bool establishConnection)
        {
            var handler1 = new InProcMessageHandlerV1();
            var handler2 = new InProcMessageHandlerV1();

            var agent1 = Create(handler1);
            var agent2 = Create(handler2);

            handler1.TargetAgent = agent2;
            handler2.TargetAgent = agent1;

            await agent1.InitializeAsync();
            await agent2.InitializeAsync();

            var result = new PairedAgents
            {
                Agent1 = agent1,
                Agent2 = agent2
            };

            if (establishConnection)
            {
                (result.Connection1, result.Connection2) = await ConnectAsync(agent1, agent2);
            }
            return result;
        }

        public static async Task<PairedAgents> CreatePairedWithRoutingAsync(Dictionary<string, string> metaData = null)
        {
            var handler1 = new InProcMessageHandlerV1();
            var handler2 = new InProcMessageHandlerV1();

            var agent1 = CreateMediator(handler1);
            var agent2 = CreateEdge(handler2, metaData);

            handler1.TargetAgent = agent2;
            handler2.TargetAgent = agent1;

            await agent1.InitializeAsync();
            await agent2.InitializeAsync();

            var result = new PairedAgents
            {
                Agent1 = agent1,
                Agent2 = agent2
            };
            return result;
        }
        
        public static async Task<PairedAgents> CreateTwoWalletsPairedWithRoutingAsync()
        {
            var handler1 = new InProcMessageHandlerV1();
            var handler2 = new InProcMessageHandlerV1();
            var handler3 = new InProcMessageHandlerV1();

            var agent1 = CreateMediator(handler1);
            var agent2 = CreateEdge(handler2);
            var agent3 = CreateEdge(handler3);

            handler1.TargetAgent = agent2;
            handler2.TargetAgent = agent1;
            handler3.TargetAgent = agent1;

            await agent1.InitializeAsync();
            await agent2.InitializeAsync();
            await agent3.InitializeAsync();

            var result = new PairedAgents
            {
                Agent1 = agent1,
                Agent2 = agent2,
                Agent3 = agent3
            };
            return result;
        }

        private static async Task<(ConnectionRecord agent1Connection, ConnectionRecord agent2Connection)> ConnectAsync(InProcAgentV1 agent1, InProcAgentV1 agent2)
        {
            var (invitation, agent1Connection) = await agent1.Provider.GetService<IConnectionService>().CreateInvitationAsync(agent1.Context, new InviteConfiguration { AutoAcceptConnection = true });

            var (request, agent2Connection) = await agent2.Provider.GetService<IConnectionService>().CreateRequestAsync(agent2.Context, invitation);
            await agent2.Provider.GetService<IMessageService>().SendAsync(
                agentContext: agent2.Context,
                message: request,
                recipientKey: invitation.RecipientKeys.First(),
                endpointUri: agent2Connection.Endpoint.Uri,
                routingKeys: agent2Connection.Endpoint.Verkey,
                senderKey: agent2Connection.MyVk);

            agent1Connection = await agent1.Provider.GetService<IWalletRecordService>().GetAsync<ConnectionRecord>(agent1.Context.AriesStorage, agent1Connection.Id);
            agent2Connection = await agent2.Provider.GetService<IWalletRecordService>().GetAsync<ConnectionRecord>(agent2.Context.AriesStorage, agent2Connection.Id);

            return (agent1Connection, agent2Connection);
        }

        private static InProcAgentV1 Create(HttpMessageHandler handler) =>
            new InProcAgentV1(new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.Configure<ConsoleLifetimeOptions>(options =>
                        options.SuppressStatusMessages = true);
                    services.AddAriesFramework(builder => builder
                        .RegisterAgent(options =>
                        {
                            options.GenesisFilename = Path.GetFullPath("pool_genesis.txn");
                            options.PoolName = "TestPool";
                            options.WalletConfiguration.Id = Guid.NewGuid().ToString();
                            options.WalletCredentials.Key = "test";
                            options.EndpointUri = "http://test";
                            options.RevocationRegistryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                        }));
                    services.AddSingleton<IHttpClientFactory>(new InProcFactory(handler));
                    services.AddSingleton<IStorageService, DefaultStorageService>();
                    services.AddMessageHandler<DefaultStoreBackupHandler>();
                    services.AddMessageHandler<RetrieveBackupHandler>();
                }).Build());

        private static InProcAgentV1 CreateEdge(HttpMessageHandler handler, Dictionary<string, string> metaData = null) =>
            new InProcAgentV1(new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.Configure<ConsoleLifetimeOptions>(options =>
                        options.SuppressStatusMessages = true);
                    services.AddAriesFramework(builder => builder
                        .RegisterEdgeAgent(options =>
                        {
                            options.GenesisFilename = Path.GetFullPath("pool_genesis.txn");
                            options.PoolName = "TestPool";
                            options.WalletConfiguration.Id = Guid.NewGuid().ToString();
                            options.WalletCredentials.Key = "test";
                            options.EndpointUri = "http://test";
                            options.MetaData = metaData;
                        }));
                    services.AddSingleton<IHttpClientFactory>(new InProcFactory(handler));
                }).Build());

        private static InProcAgentV1 CreateMediator(HttpMessageHandler handler) =>
            new InProcMediatorAgentV1(new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.Configure<ConsoleLifetimeOptions>(options =>
                        options.SuppressStatusMessages = true);
                    services.AddAriesFramework(builder => builder
                        .RegisterMediatorAgent(options =>
                        {
                            options.GenesisFilename = Path.GetFullPath("pool_genesis.txn");
                            options.PoolName = "TestPool";
                            options.WalletConfiguration.Id = Guid.NewGuid().ToString();
                            options.WalletCredentials.Key = "test";
                            options.EndpointUri = "http://test";
                        }));
                    services.AddSingleton<IHttpClientFactory>(new InProcFactory(handler));
                }).Build());

        #endregion

        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            await Host.StartAsync();
            Context = await Host.Services.GetService<IAgentProvider>().GetContextAsync();
        }

        /// <inheritdoc />
        public Task DisposeAsync()
        {
            Host.StopAsync(TimeSpan.FromSeconds(10));
            Host.Dispose();
            return Task.CompletedTask;
        }

        public class PairedAgents
        {
            public InProcAgentV1 Agent1 { get; set; }

            public InProcAgentV1 Agent2 { get; set; }
            public InProcAgentV1 Agent3 { get; set; }

            public ConnectionRecord Connection1 { get; set; }

            public ConnectionRecord Connection2 { get; set; }
        }

        public class InProcFactory : IHttpClientFactory
        {
            public InProcFactory(HttpMessageHandler handler)
            {
                Handler = handler;
            }

            public HttpMessageHandler Handler { get; set; }

            /// <inheritdoc />
            public HttpClient CreateClient(string name)
            {
                return new HttpClient(Handler);
            }
        }
    }

    public class InProcAgentV2 : AgentBase, IAsyncLifetime
    {
        /// <inheritdoc />
        public InProcAgentV2(IHost host)
            : base(host.Services.GetService<IServiceProvider>())
        {
            Host = host;
        }

        public IHost Host { get; }
        public IAgentContext Context { get; private set; }
        public IWalletRecordService Records => Host.Services.GetService<IWalletRecordService>();
        public IConnectionService Connections => Host.Services.GetService<IConnectionService>();
        public IMessageService Messages => Host.Services.GetService<IMessageService>();
        public IPaymentService Payments => Host.Services.GetService<IPaymentService>();

        internal Task<MessageContext> HandleAsync(byte[] data) =>
            ProcessAsync(Context, new PackedMessageContext(data));

        /// <inheritdoc />
        protected override void ConfigureHandlers()
        {
            AddConnectionHandler();
            AddCredentialHandler();
            AddDidExchangeHandler();
            AddDiscoveryHandler();
            AddDiscoveryHandler();
            AddForwardHandler();
            AddProofHandler();
            AddRevocationNotificationHandler();
            AddBasicMessageHandler();
            AddHandler<RetrieveBackupHandler>();
            AddHandler<DefaultStoreBackupHandler>();
        }

        #region Factory methods

        public static async Task<PairedAgentsV2> CreatePairedAsync(bool establishConnection)
        {
            var handler1 = new InProcMessageHandlerV2();
            var handler2 = new InProcMessageHandlerV2();

            var agent1 = Create(handler1, TestConstants.TestSingleWalletV2IssuerConfig, TestConstants.TestSingelWalletV2IssuerCreds);
            var agent2 = Create(handler2, TestConstants.TestSingleWalletV2HolderConfig, TestConstants.TestSingelWalletV2HolderCreds);

            handler1.TargetAgent = agent2;
            handler2.TargetAgent = agent1;

            await agent1.InitializeAsync();
            await agent2.InitializeAsync();

            var result = new PairedAgentsV2
            {
                Agent1 = agent1,
                Agent2 = agent2
            };

            if (establishConnection)
            {
                (result.Connection1, result.Connection2) = await ConnectAsync(agent1, agent2);
            }
            return result;
        }

        public static async Task<PairedAgentsV2> CreatePairedWithRoutingAsync(Dictionary<string, string> metaData = null)
        {
            var handler1 = new InProcMessageHandlerV2();
            var handler2 = new InProcMessageHandlerV2();

            var agent1 = CreateMediator(handler1);
            var agent2 = CreateEdge(handler2, metaData);

            handler1.TargetAgent = agent2;
            handler2.TargetAgent = agent1;

            await agent1.InitializeAsync();
            await agent2.InitializeAsync();

            var result = new PairedAgentsV2
            {
                Agent1 = agent1,
                Agent2 = agent2
            };
            return result;
        }

        public static async Task<PairedAgentsV2> CreateTwoWalletsPairedWithRoutingAsync()
        {
            var handler1 = new InProcMessageHandlerV2();
            var handler2 = new InProcMessageHandlerV2();
            var handler3 = new InProcMessageHandlerV2();

            var agent1 = CreateMediator(handler1);
            var agent2 = CreateEdge(handler2);
            var agent3 = CreateEdge(handler3);

            handler1.TargetAgent = agent2;
            handler2.TargetAgent = agent1;
            handler3.TargetAgent = agent1;

            await agent1.InitializeAsync();
            await agent2.InitializeAsync();
            await agent3.InitializeAsync();

            var result = new PairedAgentsV2
            {
                Agent1 = agent1,
                Agent2 = agent2,
                Agent3 = agent3
            };
            return result;
        }

        private static async Task<(ConnectionRecord agent1Connection, ConnectionRecord agent2Connection)> ConnectAsync(InProcAgentV2 agent1, InProcAgentV2 agent2)
        {
            var (invitation, agent1Connection) = await agent1.Provider.GetService<IConnectionService>().CreateInvitationAsync(agent1.Context, new InviteConfiguration { AutoAcceptConnection = true });

            var (request, agent2Connection) = await agent2.Provider.GetService<IConnectionService>().CreateRequestAsync(agent2.Context, invitation);
            await agent2.Provider.GetService<IMessageService>().SendAsync(
                agentContext: agent2.Context,
                message: request,
                recipientKey: invitation.RecipientKeys.First(),
                endpointUri: agent2Connection.Endpoint.Uri,
                routingKeys: agent2Connection.Endpoint.Verkey,
                senderKey: agent2Connection.MyVk);

            agent1Connection = await agent1.Provider.GetService<IWalletRecordService>().GetAsync<ConnectionRecord>(agent1.Context.AriesStorage, agent1Connection.Id);
            agent2Connection = await agent2.Provider.GetService<IWalletRecordService>().GetAsync<ConnectionRecord>(agent2.Context.AriesStorage, agent2Connection.Id);

            return (agent1Connection, agent2Connection);
        }

        private static InProcAgentV2 Create(HttpMessageHandler handler, WalletConfiguration config, WalletCredentials creds) =>
            new InProcAgentV2(new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.Configure<ConsoleLifetimeOptions>(options =>
                        options.SuppressStatusMessages = true);
                    services.AddAriesFrameworkV2(builder => builder
                        .RegisterAgentV2(options =>
                        {
                            options.GenesisFilename = Path.GetFullPath("pool_genesis.txn");
                            options.PoolName = "TestPool";
                            options.WalletConfiguration = config;
                            options.WalletCredentials = creds;
                            options.EndpointUri = "http://test";
                            options.RevocationRegistryDirectory = Path.GetTempPath(); // Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                        }));
                    services.AddSingleton<IHttpClientFactory>(new InProcFactoryV2(handler));
                    services.AddSingleton<IStorageService, DefaultStorageService>();
                    services.AddMessageHandler<DefaultStoreBackupHandler>();
                    services.AddMessageHandler<RetrieveBackupHandler>();
                }).Build());

        private static InProcAgentV2 CreateEdge(HttpMessageHandler handler, Dictionary<string, string> metaData = null) =>
            new InProcAgentV2(new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.Configure<ConsoleLifetimeOptions>(options =>
                        options.SuppressStatusMessages = true);
                    services.AddAriesFrameworkV2(builder => builder
                        .RegisterEdgeAgentV2(options =>
                        {
                            options.GenesisFilename = Path.GetFullPath("pool_genesis.txn");
                            options.PoolName = "TestPool";
                            options.WalletConfiguration = TestConstants.TestWalletV2EdgeConfig;
                            options.WalletCredentials = TestConstants.TestWalletV2EdgeCreds;
                            options.EndpointUri = "http://test";
                            options.MetaData = metaData;
                        }));
                    services.AddSingleton<IHttpClientFactory>(new InProcFactoryV2(handler));
                }).Build());

        private static InProcAgentV2 CreateMediator(HttpMessageHandler handler) =>
            new InProcMediatorAgentV2(new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.Configure<ConsoleLifetimeOptions>(options =>
                        options.SuppressStatusMessages = true);
                    services.AddAriesFrameworkV2(builder => builder
                        .RegisterMediatorAgent(options =>
                        {
                            options.GenesisFilename = Path.GetFullPath("pool_genesis.txn");
                            options.PoolName = "TestPool";
                            options.WalletConfiguration = TestConstants.TestWalletV2MediatorConfig;
                            options.WalletCredentials = TestConstants.TestWalletV2MediatorCreds;
                            options.EndpointUri = "http://test";
                        }));
                    services.AddSingleton<IHttpClientFactory>(new InProcFactoryV2(handler));
                }).Build());

        #endregion

        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            await Host.StartAsync();
            Context = await Host.Services.GetService<IAgentProvider>().GetContextAsync();
        }

        /// <inheritdoc />
        public async Task DisposeAsync()
        {
            await Host.StopAsync(TimeSpan.FromSeconds(10));
            var walletOptions = Host.Services.GetService<IOptions<AgentOptions>>().Value;
            var walletService = Host.Services.GetService<IWalletService>();
            await walletService.DeleteWalletAsync(walletOptions.WalletConfiguration, walletOptions.WalletCredentials);
            Host.Dispose();
        }

        public class PairedAgentsV2
        {
            public InProcAgentV2 Agent1 { get; set; }

            public InProcAgentV2 Agent2 { get; set; }
            public InProcAgentV2 Agent3 { get; set; }

            public ConnectionRecord Connection1 { get; set; }

            public ConnectionRecord Connection2 { get; set; }
        }

        public class InProcFactoryV2 : IHttpClientFactory
        {
            public InProcFactoryV2(HttpMessageHandler handler)
            {
                Handler = handler;
            }

            public HttpMessageHandler Handler { get; set; }

            /// <inheritdoc />
            public HttpClient CreateClient(string name)
            {
                return new HttpClient(Handler);
            }
        }
    }

    ////////////////////////////////////
    ///

    public static class InProcAgentV1V2
    {
        #region Factory methods

        public static async Task<PairedAgentsV1V2> CreatePairedAsync(bool establishConnection)
        {
            var handler1 = new InProcMessageHandlerV2();
            var handler2 = new InProcMessageHandlerV1();

            InProcAgentV1 agent1 = CreateV1(handler1);
            InProcAgentV2 agent2 = CreateV2(handler2, TestConstants.TestSingleWalletV2HolderConfig, TestConstants.TestSingelWalletV2HolderCreds);

            handler1.TargetAgent = agent2;
            handler2.TargetAgent = agent1;

            await agent1.InitializeAsync();
            await agent2.InitializeAsync();

            var result = new PairedAgentsV1V2
            {
                Agent1 = agent1,
                Agent2 = agent2
            };

            if (establishConnection)
            {
                (result.Connection1, result.Connection2) = await ConnectAsync(agent1, agent2);
            }
            return result;
        }

        /**
        public static async Task<PairedAgentsV1V2> CreatePairedWithRoutingAsync(Dictionary<string, string> metaData = null)
        {
            var handler1 = new InProcMessageHandlerV2();
            var handler2 = new InProcMessageHandlerV1();

            var agent1 = CreateMediatorV1(handler1);
            var agent2 = CreateEdgeV2(handler2, metaData);

            handler1.TargetAgent = agent2;
            handler2.TargetAgent = agent1;

            await agent1.InitializeAsync();
            await agent2.InitializeAsync();

            var result = new PairedAgentsV1V2
            {
                Agent1 = agent1,
                Agent2 = agent2
            };
            return result;
        }
        **/

        /**
        public static async Task<PairedAgentsV1V2> CreateTwoWalletsPairedWithRoutingAsync()
        {
            var handler1 = new InProcMessageHandlerV2();
            var handler2 = new InProcMessageHandlerV2();
            var handler3 = new InProcMessageHandlerV2();

            var agent1 = CreateMediator(handler1);
            var agent2 = CreateEdge(handler2);
            var agent3 = CreateEdge(handler3);

            handler1.TargetAgent = agent2;
            handler2.TargetAgent = agent1;
            handler3.TargetAgent = agent1;

            await agent1.InitializeAsync();
            await agent2.InitializeAsync();
            await agent3.InitializeAsync();

            var result = new PairedAgentsV2
            {
                Agent1 = agent1,
                Agent2 = agent2,
                Agent3 = agent3
            };
            return result;
        }**/

        private static async Task<(ConnectionRecord agent1Connection, ConnectionRecord agent2Connection)> ConnectAsync(InProcAgentV1 agent1, InProcAgentV2 agent2)
        {
            var (invitation, agent1Connection) = await agent1.Provider.GetService<IConnectionService>().CreateInvitationAsync(agent1.Context, new InviteConfiguration { AutoAcceptConnection = true });

            var (request, agent2Connection) = await agent2.Provider.GetService<IConnectionService>().CreateRequestAsync(agent2.Context, invitation);
            await agent2.Provider.GetService<IMessageService>().SendAsync(
                agentContext: agent2.Context,
                message: request,
                recipientKey: invitation.RecipientKeys.First(),
                endpointUri: agent2Connection.Endpoint.Uri,
                routingKeys: agent2Connection.Endpoint.Verkey,
                senderKey: agent2Connection.MyVk);

            agent1Connection = await agent1.Provider.GetService<IWalletRecordService>().GetAsync<ConnectionRecord>(agent1.Context.AriesStorage, agent1Connection.Id);
            agent2Connection = await agent2.Provider.GetService<IWalletRecordService>().GetAsync<ConnectionRecord>(agent2.Context.AriesStorage, agent2Connection.Id);

            return (agent1Connection, agent2Connection);
        }

        private static InProcAgentV2 CreateV2(HttpMessageHandler handler, WalletConfiguration config, WalletCredentials creds) =>
            new InProcAgentV2(new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.Configure<ConsoleLifetimeOptions>(options =>
                        options.SuppressStatusMessages = true);
                    services.AddAriesFrameworkV2(builder => builder
                        .RegisterAgentV2(options =>
                        {
                            options.GenesisFilename = Path.GetFullPath("pool_genesis.txn");
                            options.PoolName = "TestPool";
                            options.WalletConfiguration = config;
                            options.WalletCredentials = creds;
                            options.EndpointUri = "http://test";
                            options.RevocationRegistryDirectory = Path.GetTempPath(); // Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                        }));
                    services.AddSingleton<IHttpClientFactory>(new InProcFactoryV2(handler));
                    services.AddSingleton<IStorageService, DefaultStorageService>();
                    services.AddMessageHandler<DefaultStoreBackupHandler>();
                    services.AddMessageHandler<RetrieveBackupHandler>();
                }).Build());

        private static InProcAgentV2 CreateEdgeV2(HttpMessageHandler handler, Dictionary<string, string> metaData = null) =>
            new InProcAgentV2(new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.Configure<ConsoleLifetimeOptions>(options =>
                        options.SuppressStatusMessages = true);
                    services.AddAriesFrameworkV2(builder => builder
                        .RegisterEdgeAgentV2(options =>
                        {
                            options.GenesisFilename = Path.GetFullPath("pool_genesis.txn");
                            options.PoolName = "TestPool";
                            options.WalletConfiguration = TestConstants.TestWalletV2EdgeConfig;
                            options.WalletCredentials = TestConstants.TestWalletV2EdgeCreds;
                            options.EndpointUri = "http://test";
                            options.MetaData = metaData;
                        }));
                    services.AddSingleton<IHttpClientFactory>(new InProcFactoryV2(handler));
                }).Build());

        private static InProcAgentV2 CreateMediatorV2(HttpMessageHandler handler) =>
            new InProcMediatorAgentV2(new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.Configure<ConsoleLifetimeOptions>(options =>
                        options.SuppressStatusMessages = true);
                    services.AddAriesFrameworkV2(builder => builder
                        .RegisterMediatorAgent(options =>
                        {
                            options.GenesisFilename = Path.GetFullPath("pool_genesis.txn");
                            options.PoolName = "TestPool";
                            options.WalletConfiguration = TestConstants.TestWalletV2MediatorConfig;
                            options.WalletCredentials = TestConstants.TestWalletV2MediatorCreds;
                            options.EndpointUri = "http://test";
                        }));
                    services.AddSingleton<IHttpClientFactory>(new InProcFactoryV2(handler));
                }).Build());

        private static InProcAgentV1 CreateV1(HttpMessageHandler handler) =>
            new InProcAgentV1(new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.Configure<ConsoleLifetimeOptions>(options =>
                        options.SuppressStatusMessages = true);
                    services.AddAriesFramework(builder => builder
                        .RegisterAgent(options =>
                        {
                            options.GenesisFilename = Path.GetFullPath("pool_genesis.txn");
                            options.PoolName = "TestPool";
                            options.WalletConfiguration.Id = Guid.NewGuid().ToString();
                            options.WalletCredentials.Key = "test";
                            options.EndpointUri = "http://test";
                            options.RevocationRegistryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                        }));
                    services.AddSingleton<IHttpClientFactory>(new InProcFactory(handler));
                    services.AddSingleton<IStorageService, DefaultStorageService>();
                    services.AddMessageHandler<DefaultStoreBackupHandler>();
                    services.AddMessageHandler<RetrieveBackupHandler>();
                }).Build());

        private static InProcAgentV1 CreateEdgeV1(HttpMessageHandler handler, Dictionary<string, string> metaData = null) =>
            new InProcAgentV1(new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.Configure<ConsoleLifetimeOptions>(options =>
                        options.SuppressStatusMessages = true);
                    services.AddAriesFramework(builder => builder
                        .RegisterEdgeAgent(options =>
                        {
                            options.GenesisFilename = Path.GetFullPath("pool_genesis.txn");
                            options.PoolName = "TestPool";
                            options.WalletConfiguration.Id = Guid.NewGuid().ToString();
                            options.WalletCredentials.Key = "test";
                            options.EndpointUri = "http://test";
                            options.MetaData = metaData;
                        }));
                    services.AddSingleton<IHttpClientFactory>(new InProcFactory(handler));
                }).Build());

        private static InProcAgentV1 CreateMediatorV1(HttpMessageHandler handler) =>
            new InProcMediatorAgentV1(new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.Configure<ConsoleLifetimeOptions>(options =>
                        options.SuppressStatusMessages = true);
                    services.AddAriesFramework(builder => builder
                        .RegisterMediatorAgent(options =>
                        {
                            options.GenesisFilename = Path.GetFullPath("pool_genesis.txn");
                            options.PoolName = "TestPool";
                            options.WalletConfiguration.Id = Guid.NewGuid().ToString();
                            options.WalletCredentials.Key = "test";
                            options.EndpointUri = "http://test";
                        }));
                    services.AddSingleton<IHttpClientFactory>(new InProcFactory(handler));
                }).Build());

        #endregion

        public class PairedAgentsV1V2
        {
            public InProcAgentV1 Agent1 { get; set; }

            public InProcAgentV2 Agent2 { get; set; }

            public ConnectionRecord Connection1 { get; set; }

            public ConnectionRecord Connection2 { get; set; }
        }
    }
}
