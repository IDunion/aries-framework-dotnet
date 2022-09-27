using FluentAssertions;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Agents.Edge;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Features.Handshakes.Common;
using Hyperledger.Aries.Features.Handshakes.Connection;
using Hyperledger.Aries.Ledger;
using Hyperledger.Aries.Routing;
using Hyperledger.Aries.Routing.Edge;
using Hyperledger.Aries.Routing.Mediator;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.TestHarness;
using Hyperledger.TestHarness.Mock;
using Hyperledger.TestHarness.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Hyperledger.Aries.Tests.Routing
{

    /// <summary>
    /// Routing Tests
    /// </summary>
    [Trait("Category", "DefaultV1")]
    public class RoutingTestsV1
    {
        [Fact(DisplayName = "Provision and connect a mediator and edge agent")]
        public async Task CreatePairedAgentsWithRouting()
        {
            var pair = await InProcAgentV1.CreatePairedWithRoutingAsync();

            var connections1 = await pair.Agent1.Connections.ListAsync(pair.Agent1.Context);
            var invitation1 = connections1.FirstOrDefault(x => x.State == ConnectionState.Invited);

            var connection1 = connections1.FirstOrDefault(x => x.Id != invitation1.Id);
            var connection2 = (await pair.Agent2.Connections.ListAsync(pair.Agent2.Context)).FirstOrDefault();

            var provisioning1 = await pair.Agent1.Host.Services.GetRequiredService<IProvisioningService>()
                .GetProvisioningAsync(pair.Agent1.Context.AriesStorage);
            var provisioning2 = await pair.Agent2.Host.Services.GetRequiredService<IProvisioningService>()
                .GetProvisioningAsync(pair.Agent2.Context.AriesStorage);

            // Connections exist
            Assert.NotNull(invitation1);
            Assert.NotNull(connection1);
            Assert.NotNull(connection2);

            // The two connections are connected in the correct state
            Assert.Equal(ConnectionState.Connected, connection1.State);
            Assert.Equal(ConnectionState.Connected, connection2.State);

            // Check mediator and edge provisioning record states
            Assert.Equal(provisioning1.GetTag(MediatorProvisioningService.EdgeInvitationTagName), invitation1.Id);
            Assert.Equal(provisioning2.GetTag(EdgeProvisioningService.MediatorConnectionIdTagName), connection2.Id);

            string inboxId = connection1.GetTag("InboxId");
            IWalletRecordService recordService = pair.Agent1.Host.Services.GetRequiredService<IWalletRecordService>();
            InboxRecord inboxRecord = await recordService.GetAsync<InboxRecord>(pair.Agent1.Context.AriesStorage, inboxId);
            inboxRecord.GetTag("tag").Should().BeNull();
        }

        [Fact(DisplayName = "Provision and connect a mediator and edge agent with metadata provided")]
        public async Task CreatePairedAgentsWithRoutingAndMetadata()
        {
            Dictionary<string, string> metaData = new Dictionary<string, string>()
            {
                { "tag", "value" }
            };
            var pair = await InProcAgentV1.CreatePairedWithRoutingAsync(metaData);

            var connections1 = await pair.Agent1.Connections.ListAsync(pair.Agent1.Context);
            var invitation1 = connections1.FirstOrDefault(x => x.State == ConnectionState.Invited);

            var connection1 = connections1.FirstOrDefault(x => x.Id != invitation1.Id);
            var connection2 = (await pair.Agent2.Connections.ListAsync(pair.Agent2.Context)).FirstOrDefault();

            var provisioning1 = await pair.Agent1.Host.Services.GetRequiredService<IProvisioningService>()
                .GetProvisioningAsync(pair.Agent1.Context.AriesStorage);
            var provisioning2 = await pair.Agent2.Host.Services.GetRequiredService<IProvisioningService>()
                .GetProvisioningAsync(pair.Agent2.Context.AriesStorage);

            // Connections exist
            invitation1.Should().NotBeNull();
            connection1.Should().NotBeNull();
            connection2.Should().NotBeNull();

            // The two connections are connected in the correct state
            ConnectionState.Connected.Should().Be(connection1.State);
            ConnectionState.Connected.Should().Be(connection2.State);

            // Check mediator and edge provisioning record states
            provisioning1.GetTag(MediatorProvisioningService.EdgeInvitationTagName).Should().Be(invitation1.Id);
            provisioning2.GetTag(EdgeProvisioningService.MediatorConnectionIdTagName).Should().Be(connection2.Id);

            string inboxId = connection1.GetTag("InboxId");
            IWalletRecordService recordService = pair.Agent1.Host.Services.GetRequiredService<IWalletRecordService>();
            InboxRecord inboxRecord = await recordService.GetAsync<InboxRecord>(pair.Agent1.Context.AriesStorage, inboxId);
            inboxRecord.GetTag("tag").Should().Be(metaData["tag"]);
        }
    }

    [Trait("Category", "DefaultV2")]
    public class RoutingTestsV2 : IAsyncLifetime
    {
        private IMessageService _messageService;
        private IHostedService _mediatorProvisioningService;
        private IEdgeProvisioningService _edgeProvisioningService;

        private readonly WalletConfiguration _walletConfig = TestConstants.TestSingleWalletV2WalletConfig;
        private readonly WalletCredentials _walletCredentials = TestConstants.TestSingelWalletV2WalletCreds;

        private IWalletService _walletService;
        private IWalletRecordService _recordService;
        private IAgentProvider _agentProvider;
        private IProvisioningService _provisioningService;
        private IAgentContext _agentContext;
        private IConnectionService _connectionService;

        private Mock<IProvisioningService> _mockProvisioningService;

        public async Task InitializeAsync()
        {
            IOptions<AgentOptions> agentOptions = Options.Create<AgentOptions>(new AgentOptions {
                WalletConfiguration = _walletConfig,
                WalletCredentials = _walletCredentials,
                EndpointUri = TestConstants.DefaultMockUri
            });
            IEventAggregator eventAggregator = new EventAggregator();
            IPoolService poolService = new DefaultPoolServiceV2();
            _messageService = new Mock<IMessageService>().Object;
            Mock<IEdgeClientService> mockEdgeClientService = new();
            mockEdgeClientService.Setup(x => x.DiscoverConfigurationAsync(It.IsAny<string>())).Returns(Task.FromResult(new AgentPublicConfiguration
            {
                ServiceEndpoint = TestConstants.DefaultMockUri,
                RoutingKey = TestConstants.DefaultVerkey,
                Invitation = new Features.Handshakes.Connection.Models.ConnectionInvitationMessage()
            }
            )) ;

            _mockProvisioningService = new();
            

            _recordService = new DefaultWalletRecordServiceV2();
            _walletService = new DefaultWalletServiceV2();
            _agentContext = await AgentUtils.CreateV2(_walletService, _walletConfig, _walletCredentials);


            _agentProvider = new DefaultAgentProvider(
                agentOptions,
                _agentContext.Agent,
                _walletService,
                poolService
                );
            _provisioningService = new DefaultProvisioningServiceV2(
                _recordService,
                _walletService,
                agentOptions
                );
            _connectionService = new DefaultConnectionServiceV2(
                eventAggregator,
                _recordService,
                _provisioningService,
                new Mock<ILogger<DefaultConnectionServiceV2>>().Object
                );

            _mediatorProvisioningService = new MediatorProvisioningServiceV2(
                _connectionService,
                _provisioningService,
                _recordService,
                _agentProvider
                );

            _edgeProvisioningService = new EdgeProvisioningServiceV2(
                _mockProvisioningService.Object,
                _connectionService,
                _messageService,
                mockEdgeClientService.Object,
                _recordService,
                _agentProvider,
                agentOptions
                );
        }

        [Fact(DisplayName = "Provision and connect a mediator and edge agent")]
        public async Task CreatePairedAgentsWithRouting()
        {
            var pair = await InProcAgentV2.CreatePairedWithRoutingAsync();

            var connections1 = await pair.Agent1.Connections.ListAsync(pair.Agent1.Context);
            var invitation1 = connections1.FirstOrDefault(x => x.State == ConnectionState.Invited);

            var connection1 = connections1.FirstOrDefault(x => x.Id != invitation1.Id);
            var connection2 = (await pair.Agent2.Connections.ListAsync(pair.Agent2.Context)).FirstOrDefault();

            var provisioning1 = await pair.Agent1.Host.Services.GetRequiredService<IProvisioningService>()
                .GetProvisioningAsync(pair.Agent1.Context.AriesStorage);
            var provisioning2 = await pair.Agent2.Host.Services.GetRequiredService<IProvisioningService>()
                .GetProvisioningAsync(pair.Agent2.Context.AriesStorage);

            // Connections exist
            Assert.NotNull(invitation1);
            Assert.NotNull(connection1);
            Assert.NotNull(connection2);

            // The two connections are connected in the correct state
            Assert.Equal(ConnectionState.Connected, connection1.State);
            Assert.Equal(ConnectionState.Connected, connection2.State);

            // Check mediator and edge provisioning record states
            Assert.Equal(provisioning1.GetTag(MediatorProvisioningServiceV2.EdgeInvitationTagName), invitation1.Id);
            Assert.Equal(provisioning2.GetTag(EdgeProvisioningServiceV2.MediatorConnectionIdTagName), connection2.Id);

            string inboxId = connection1.GetTag("InboxId");
            IWalletRecordService recordService = pair.Agent1.Host.Services.GetRequiredService<IWalletRecordService>();
            InboxRecord inboxRecord = await recordService.GetAsync<InboxRecord>(pair.Agent1.Context.AriesStorage, inboxId);
            inboxRecord.GetTag("tag").Should().BeNull();
        }

        [Fact(DisplayName = "Provision and connect a mediator and edge agent with metadata provided")]
        public async Task CreatePairedAgentsWithRoutingAndMetadata()
        {
            Dictionary<string, string> metaData = new Dictionary<string, string>()
            {
                { "tag", "value" }
            };
            var pair = await InProcAgentV2.CreatePairedWithRoutingAsync(metaData);

            var connections1 = await pair.Agent1.Connections.ListAsync(pair.Agent1.Context);
            var invitation1 = connections1.FirstOrDefault(x => x.State == ConnectionState.Invited);

            var connection1 = connections1.FirstOrDefault(x => x.Id != invitation1.Id);
            var connection2 = (await pair.Agent2.Connections.ListAsync(pair.Agent2.Context)).FirstOrDefault();

            var provisioning1 = await pair.Agent1.Host.Services.GetRequiredService<IProvisioningService>()
                .GetProvisioningAsync(pair.Agent1.Context.AriesStorage);
            var provisioning2 = await pair.Agent2.Host.Services.GetRequiredService<IProvisioningService>()
                .GetProvisioningAsync(pair.Agent2.Context.AriesStorage);

            // Connections exist
            invitation1.Should().NotBeNull();
            connection1.Should().NotBeNull();
            connection2.Should().NotBeNull();

            // The two connections are connected in the correct state
            ConnectionState.Connected.Should().Be(connection1.State);
            ConnectionState.Connected.Should().Be(connection2.State);

            // Check mediator and edge provisioning record states
            provisioning1.GetTag(MediatorProvisioningServiceV2.EdgeInvitationTagName).Should().Be(invitation1.Id);
            provisioning2.GetTag(EdgeProvisioningServiceV2.MediatorConnectionIdTagName).Should().Be(connection2.Id);

            string inboxId = connection1.GetTag("InboxId");
            IWalletRecordService recordService = pair.Agent1.Host.Services.GetRequiredService<IWalletRecordService>();
            InboxRecord inboxRecord = await recordService.GetAsync<InboxRecord>(pair.Agent1.Context.AriesStorage, inboxId);
            inboxRecord.GetTag("tag").Should().Be(metaData["tag"]);
        }

        [Fact(DisplayName = "MediatorProvisioningService updates records at start")]
        public async Task UpdateProvisioningRecords()
        {
            //Arrange
            await _provisioningService.ProvisionAgentAsync();

            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken token = source.Token;

            //Act
            await _mediatorProvisioningService.StartAsync(token);

            ProvisioningRecord updatedProvsioningRecord = await _provisioningService.GetProvisioningAsync(_agentContext.AriesStorage);
            var invitationId = updatedProvsioningRecord.GetTag("EdgeInvitationId");
            ConnectionRecord createdInvitationRecord = await _recordService.GetAsync<ConnectionRecord>(_agentContext.AriesStorage, invitationId);

            //Assert
            invitationId.Should().NotBe("");
            createdInvitationRecord.Should().NotBe(null);
        }

        [Fact(DisplayName = "EdgeProvisioningService creates Inbox when ConnectionIdTag is set")]
        public async Task ProvisioningWorksWithConnectionIdTag()
        {
            // Arrange
            _mockProvisioningService.Setup(x => x.GetProvisioningAsync(It.IsAny<AriesStorage>())).Returns(Task.FromResult(new ProvisioningRecord
            {
                Tags = new Dictionary<string, string>() { ["MediatorConnectionId"] = "connectionId" }
            }));

            // Act
            var act = async () => await _edgeProvisioningService.ProvisionAsync();

            // Assert
            await act.Should().NotThrowAsync<Exception>();
        }

        public async Task DisposeAsync()
        {
            if (_agentContext != null)
            {
                await _walletService.DeleteWalletAsync(_walletConfig, _walletCredentials);
            }
        }
    }
}
