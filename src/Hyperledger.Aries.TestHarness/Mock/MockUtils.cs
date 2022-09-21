﻿using System.Net.Http;
using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Ledger.Models;
using Hyperledger.Aries.Storage;
using Hyperledger.TestHarness.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Hyperledger.TestHarness.Mock
{
    public class MockUtils
    {
        public static async Task<MockAgent> CreateAsync(string agentName, WalletConfiguration configuration, WalletCredentials credentials, MockAgentHttpHandler handler, string issuerSeed = null, bool useMessageTypesHttps = false)
        {
            var services = new ServiceCollection();

            services.AddAriesFramework();
            services.AddDefaultMessageHandlers();
            services.AddLogging();
            services.AddSingleton<MockAgentMessageProcessor>();
            services.AddSingleton<IHttpClientFactory>(new InProcAgent.InProcFactory(handler));

            return await CreateAsync(agentName, configuration, credentials, services, issuerSeed, useMessageTypesHttps);
        }

        public static async Task<MockAgent> CreateAsync(string agentName, WalletConfiguration configuration, WalletCredentials credentials, ServiceCollection services, string issuerSeed = null, bool useMessageTypesHttps = false)
        {
            var provider = services.BuildServiceProvider();

            await provider.GetService<IProvisioningService>()
                .ProvisionAgentAsync(new AgentOptions
                {
                    EndpointUri = $"http://{agentName}",
                    IssuerKeySeed = issuerSeed,
                    WalletConfiguration = configuration,
                    WalletCredentials = credentials,
                    UseMessageTypesHttps = useMessageTypesHttps
                });

            return new MockAgent(agentName, provider)
            {
                Context = new DefaultAgentContext
                {
                    AriesStorage = await provider.GetService<IWalletService>().GetWalletAsync(configuration, credentials),
                    Pool = PoolAwaitable.FromPool(new AriesPool(await PoolUtils.GetPoolAsync())),
                    SupportedMessages = AgentUtils.GetDefaultMessageTypes(),
                    UseMessageTypesHttps = useMessageTypesHttps
                },
                ServiceProvider = provider
            };
        }

        public static async Task Dispose(MockAgent agent)
        {
            agent.Context.AriesStorage.Wallet.Dispose();
            await agent.Context.AriesStorage.Wallet.CloseAsync();
        }
    }
}
