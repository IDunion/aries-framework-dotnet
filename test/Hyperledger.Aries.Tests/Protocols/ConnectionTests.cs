using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Models.Events;
using Hyperledger.Aries.Features.Handshakes.Common;
using Hyperledger.Aries.Features.Handshakes.Common.Dids;
using Hyperledger.Aries.Features.Handshakes.Connection;
using Hyperledger.Aries.Features.Handshakes.Connection.Models;
using Hyperledger.Aries.Runtime;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.TestHarness;
using Hyperledger.Aries.Utils;
using Hyperledger.Indy.WalletApi;
using Hyperledger.TestHarness;
using Hyperledger.TestHarness.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using static Hyperledger.Aries.Tests.SchemaServiceTests;
using Microsoft.Extensions.DependencyInjection;

namespace Hyperledger.Aries.Tests.Protocols
{
    [Trait("Category", "DefaultV1")]
    public class ConnectionTestsV1 : IClassFixture<ConnectionTestsV1.SingleTestWalletFixture>
    {
        private SingleTestWalletFixture _fixture;

        private IEventAggregator _eventAggregator;
        private IConnectionService _connectionService;
        private IProvisioningService _provisioningService;

        private readonly ConcurrentBag<AgentMessage> _messages = new ConcurrentBag<AgentMessage>();

        public ConnectionTestsV1(SingleTestWalletFixture fixture)
        {
            _fixture = fixture;
            _eventAggregator = new EventAggregator();
            _provisioningService = ServiceUtils.GetDefaultMockProvisioningService();
            _connectionService = new DefaultConnectionService(
                _eventAggregator,
                new DefaultWalletRecordService(),
                _provisioningService,
                new Mock<ILogger<DefaultConnectionService>>().Object);
        }

        public class SingleTestWalletFixture : TestSingleWallet
        {
            private readonly string _issuerConfig = $"{{\"id\":\"{Guid.NewGuid()}\"}}";
            private readonly string _holderConfig = $"{{\"id\":\"{Guid.NewGuid()}\"}}";
            private readonly string _holderConfigTwo = $"{{\"id\":\"{Guid.NewGuid()}\"}}";
            private const string Credentials = "{\"key\":\"test_wallet_key\"}";

            public IAgentContext issuerWallet;
            public IAgentContext holderWallet;
            public IAgentContext holderWalletTwo;

            public override async Task InitializeAsync()
            {
                issuerWallet = await AgentUtils.Create(_issuerConfig, Credentials);
                holderWallet = await AgentUtils.Create(_holderConfig, Credentials);
                holderWalletTwo = await AgentUtils.Create(_holderConfigTwo, Credentials);
                await base.InitializeAsync();
            }

            public override async Task DisposeAsync()
            {
                if (issuerWallet != null) await issuerWallet.AriesStorage.Wallet.CloseAsync();
                if (holderWallet != null) await holderWallet.AriesStorage.Wallet.CloseAsync();
                if (holderWalletTwo != null) await holderWalletTwo.AriesStorage.Wallet.CloseAsync();

                await Wallet.DeleteWalletAsync(_issuerConfig, Credentials);
                await Wallet.DeleteWalletAsync(_holderConfig, Credentials);
                await Wallet.DeleteWalletAsync(_holderConfigTwo, Credentials);
                await  base.DisposeAsync();
            }
        }

        [Fact]
        public async Task CanCreateInvitationAsync()
        {
            var connectionId = Guid.NewGuid().ToString();

            var (msg , record) = await _connectionService.CreateInvitationAsync(_fixture.issuerWallet,
                new InviteConfiguration { ConnectionId = connectionId });

            var connection = await _connectionService.GetAsync(_fixture.issuerWallet, connectionId);

            Assert.False(connection.MultiPartyInvitation);
            Assert.Equal(ConnectionState.Invited, connection.State);
            Assert.Equal(connectionId, connection.Id);
        }
        
        [Fact]
        public async Task CanCreateInvitationWithDidKeyFormatAsync()
        {
            var connectionId = Guid.NewGuid().ToString();

            var (msg, record) = await _connectionService.CreateInvitationAsync(_fixture.issuerWallet,
                new InviteConfiguration { ConnectionId = connectionId, UseDidKeyFormat = true});

            var connection = await _connectionService.GetAsync(_fixture.issuerWallet, connectionId);

            Assert.True(DidUtils.IsDidKey(msg.RecipientKeys.First()));
            Assert.True(DidUtils.IsDidKey(msg.RoutingKeys.First()));
            Assert.False(connection.MultiPartyInvitation);
            Assert.Equal(ConnectionState.Invited, connection.State);
            Assert.Equal(connectionId, connection.Id);
        }

        [Fact]
        public async Task CanCreateMultiPartyInvitationAsync()
        {
            var connectionId = Guid.NewGuid().ToString();

            await _connectionService.CreateInvitationAsync(_fixture.issuerWallet,
                new InviteConfiguration { ConnectionId = connectionId, MultiPartyInvitation = true });

            var connection = await _connectionService.GetAsync(_fixture.issuerWallet, connectionId);

            Assert.True(connection.MultiPartyInvitation);
            Assert.Equal(ConnectionState.Invited, connection.State);
            Assert.Equal(connectionId, connection.Id);
        }

        [Fact]
        public async Task CreateInvitiationThrowsInvalidStateNoEndpoint()
        {
            _provisioningService = ServiceUtils.GetDefaultMockProvisioningService(null, "DefaultMasterSecret", null);

            _connectionService = new DefaultConnectionService(
                _eventAggregator,
                new DefaultWalletRecordService(),
                _provisioningService,
                new Mock<ILogger<DefaultConnectionService>>().Object);

            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _connectionService.CreateInvitationAsync(_fixture.issuerWallet,
                new InviteConfiguration()));

            Assert.True(ex.ErrorCode == ErrorCode.RecordInInvalidState);
        }

        [Fact]
        public async Task CanCreateRequestWithoutEndpoint()
        {
            var provisioningService = ServiceUtils.GetDefaultMockProvisioningService(null, "DefaultMasterSecret", null);

            var connectionService = new DefaultConnectionService(
                _eventAggregator,
                new DefaultWalletRecordService(),
                provisioningService,
                new Mock<ILogger<DefaultConnectionService>>().Object);

            var (invite, _) = await _connectionService.CreateInvitationAsync(_fixture.issuerWallet,
                new InviteConfiguration());

            var (request, _) = await connectionService.CreateRequestAsync(_fixture.holderWallet, invite);

            Assert.True(request.Connection.DidDoc.Services.Count == 0);
        }

        [Fact]
        public async Task CanReceiveRequestWithoutEndpoint()
        {
            var provisioningService = ServiceUtils.GetDefaultMockProvisioningService(null, "DefaultMasterSecret", null);

            var connectionService = new DefaultConnectionService(
                _eventAggregator,
                new DefaultWalletRecordService(),
                provisioningService,
                new Mock<ILogger<DefaultConnectionService>>().Object);

            var (invite, inviteeConnection) = await _connectionService.CreateInvitationAsync(_fixture.issuerWallet,
                new InviteConfiguration());

            var (request, _) = await connectionService.CreateRequestAsync(_fixture.holderWallet, invite);

            var id = await _connectionService.ProcessRequestAsync(_fixture.issuerWallet, request, inviteeConnection);

            inviteeConnection = await _connectionService.GetAsync(_fixture.issuerWallet, id);

            Assert.True(inviteeConnection.State == ConnectionState.Negotiating);
            Assert.True(request.Connection.DidDoc.Services.Count == 0);
        }

        [Fact]
        public async Task AcceptRequestThrowsExceptionConnectionNotFound()
        {
            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _connectionService.CreateResponseAsync(_fixture.issuerWallet, "bad-connection-id"));
            Assert.True(ex.ErrorCode == ErrorCode.RecordNotFound);
        }

        [Fact]
        public async Task AcceptRequestThrowsExceptionConnectionInvalidState()
        {
            var connectionId = Guid.NewGuid().ToString();

            await _connectionService.CreateInvitationAsync(_fixture.issuerWallet,
                new InviteConfiguration { ConnectionId = connectionId, AutoAcceptConnection = false });

            //Process a connection request
            var connectionRecord = await _connectionService.GetAsync(_fixture.issuerWallet, connectionId);

            await _connectionService.ProcessRequestAsync(_fixture.issuerWallet, new ConnectionRequestMessage
            {
                Connection = new Connection
                {
                    Did = "EYS94e95kf6LXF49eARL76",
                    DidDoc = new ConnectionRecord
                    {
                        MyVk = "6vyxuqpe3UBcTmhF3Wmmye2UVroa51Lcd9smQKFB5QX1"
                    }.MyDidDoc(await _provisioningService.GetProvisioningAsync(_fixture.issuerWallet.AriesStorage))
                }
            }, connectionRecord);

            //Accept the connection request
            await _connectionService.CreateResponseAsync(_fixture.issuerWallet, connectionId);

            //Now try and accept it again
            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _connectionService.CreateResponseAsync(_fixture.issuerWallet, connectionId));

            Assert.True(ex.ErrorCode == ErrorCode.RecordInInvalidState);
        }

        [Fact]
        public async Task RevokeInvitationThrowsConnectionNotFound()
        {
            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _connectionService.RevokeInvitationAsync(_fixture.issuerWallet, "bad-connection-id"));
            Assert.True(ex.ErrorCode == ErrorCode.RecordNotFound);
        }

        [Fact]
        public async Task RevokeInvitationThrowsConnectionInvalidState()
        {
            var connectionId = Guid.NewGuid().ToString();

            await _connectionService.CreateInvitationAsync(_fixture.issuerWallet,
                new InviteConfiguration { ConnectionId = connectionId, AutoAcceptConnection = false });

            //Process a connection request
            var connectionRecord = await _connectionService.GetAsync(_fixture.issuerWallet, connectionId);

            await _connectionService.ProcessRequestAsync(_fixture.issuerWallet, new ConnectionRequestMessage
            {
                Connection = new Connection
                {
                    Did = "EYS94e95kf6LXF49eARL76",
                    DidDoc = new ConnectionRecord
                    {
                        MyVk = "6vyxuqpe3UBcTmhF3Wmmye2UVroa51Lcd9smQKFB5QX1"
                    }.MyDidDoc(await _provisioningService.GetProvisioningAsync(_fixture.issuerWallet.AriesStorage))
                }
            }, connectionRecord);

            //Accept the connection request
            await _connectionService.CreateResponseAsync(_fixture.issuerWallet, connectionId);

            //Now try and revoke invitation
            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _connectionService.RevokeInvitationAsync(_fixture.issuerWallet, connectionId));

            Assert.True(ex.ErrorCode == ErrorCode.RecordInInvalidState);
        }

        [Fact]
        public async Task CanRevokeInvitation()
        {
            var connectionId = Guid.NewGuid().ToString();

            await _connectionService.CreateInvitationAsync(_fixture.issuerWallet,
                new InviteConfiguration { ConnectionId = connectionId });

            var connection = await _connectionService.GetAsync(_fixture.issuerWallet, connectionId);

            Assert.False(connection.MultiPartyInvitation);
            Assert.Equal(ConnectionState.Invited, connection.State);
            Assert.Equal(connectionId, connection.Id);

            await _connectionService.RevokeInvitationAsync(_fixture.issuerWallet, connectionId);

            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _connectionService.CreateResponseAsync(_fixture.issuerWallet, connectionId));
            Assert.True(ex.ErrorCode == ErrorCode.RecordNotFound);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CanEstablishConnectionAsync(bool useDidKeyFormat)
        {
            var events = 0;
            _eventAggregator.GetEventByType<ServiceMessageProcessingEvent>()
                .Where(_ => (_.MessageType == MessageTypes.ConnectionRequest ||
                             _.MessageType == MessageTypes.ConnectionResponse))
                .Subscribe(_ =>
                {
                    events++;
                });


            var (connectionIssuer, connectionHolder) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _fixture.issuerWallet, _fixture.holderWallet, useDidKeyFormat: useDidKeyFormat);

            Assert.True(events == 2);

            Assert.Equal(ConnectionState.Connected, connectionIssuer.State);
            Assert.Equal(ConnectionState.Connected, connectionHolder.State);

            Assert.Equal(connectionIssuer.MyDid, connectionHolder.TheirDid);
            Assert.Equal(connectionIssuer.TheirDid, connectionHolder.MyDid);

            Assert.Equal(connectionIssuer.Endpoint.Uri, TestConstants.DefaultMockUri);
            Assert.Equal(connectionIssuer.Endpoint.Uri, TestConstants.DefaultMockUri);
        }

        [Fact]
        public async Task CanEstablishConnectionsWithMultiPartyInvitationAsync()
        {
            (var invite, var record) = await _connectionService.CreateInvitationAsync(_fixture.issuerWallet,
                new InviteConfiguration { MultiPartyInvitation = true });

            var (connectionIssuer, connectionHolderOne) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _fixture.issuerWallet, _fixture.holderWallet, invite, record.Id);

            _messages.Clear();

            var (connectionIssuerTwo, connectionHolderTwo) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _fixture.issuerWallet, _fixture.holderWalletTwo, invite, record.Id);

            Assert.Equal(ConnectionState.Connected, connectionIssuer.State);
            Assert.Equal(ConnectionState.Connected, connectionHolderOne.State);

            Assert.Equal(ConnectionState.Connected, connectionIssuerTwo.State);
            Assert.Equal(ConnectionState.Connected, connectionHolderTwo.State);

            Assert.Equal(connectionIssuer.MyDid, connectionHolderOne.TheirDid);
            Assert.Equal(connectionIssuer.TheirDid, connectionHolderOne.MyDid);

            Assert.Equal(connectionIssuerTwo.MyDid, connectionHolderTwo.TheirDid);
            Assert.Equal(connectionIssuerTwo.TheirDid, connectionHolderTwo.MyDid);

            Assert.Equal(connectionIssuer.Endpoint.Uri, TestConstants.DefaultMockUri);
            Assert.Equal(connectionIssuerTwo.Endpoint.Uri, TestConstants.DefaultMockUri);
        }
    }

    [Trait("Category", "DefaultV2")]
    public class ConnectionTestsV2 : IClassFixture<ConnectionTestsV2.SingleTestWalletFixtureV2>
    {
        private SingleTestWalletFixtureV2 _fixture;

        private IEventAggregator _eventAggregator;
        private IConnectionService _connectionService;
        private IProvisioningService _provisioningService;

        private readonly ConcurrentBag<AgentMessage> _messages = new ConcurrentBag<AgentMessage>();

        public ConnectionTestsV2(SingleTestWalletFixtureV2 fixture)
        {
            _fixture = fixture;
            _eventAggregator = new EventAggregator();
            _provisioningService = ServiceUtils.GetDefaultMockProvisioningService();
            _connectionService = new DefaultConnectionServiceV2(
                _eventAggregator,
                new DefaultWalletRecordServiceV2(),
                _provisioningService,
                new Mock<ILogger<DefaultConnectionServiceV2>>().Object);
        }

        public class SingleTestWalletFixtureV2 : TestSingleWalletV2
        {
            public IAgentContext issuerWallet;
            public IAgentContext holderWallet;
            public IAgentContext holderWalletTwo;

            public override async Task InitializeAsync()
            {
                await base.InitializeAsync();
                var walletService = Host.Services.GetService<IWalletService>();
                issuerWallet = await AgentUtils.CreateV2(walletService: walletService, config: TestConstants.TestSingleWalletV2IssuerConfig, credentials: TestConstants.TestSingelWalletV2IssuerCreds);
                holderWallet = await AgentUtils.CreateV2(walletService: walletService, config: TestConstants.TestSingleWalletV2HolderConfig, credentials: TestConstants.TestSingelWalletV2HolderCreds);
                holderWalletTwo = await AgentUtils.CreateV2(walletService: walletService, config: TestConstants.TestSingleWalletV2WalletConfig, credentials: TestConstants.TestSingelWalletV2WalletCreds);
            }

            public override async Task DisposeAsync()
            {
                var walletService = Host.Services.GetService<IWalletService>();


                if (issuerWallet != null) await walletService.DeleteWalletAsync(TestConstants.TestSingleWalletV2IssuerConfig, TestConstants.TestSingelWalletV2IssuerCreds);
                if (holderWallet != null) await walletService.DeleteWalletAsync(TestConstants.TestSingleWalletV2HolderConfig, TestConstants.TestSingelWalletV2HolderCreds);
                if (holderWalletTwo != null) await walletService.DeleteWalletAsync(TestConstants.TestSingleWalletV2WalletConfig, TestConstants.TestSingelWalletV2WalletCreds);
                //if (issuerWallet != null) await issuerWallet.AriesStorage.Wallet.CloseAsync();
                //if (holderWallet != null) await holderWallet.AriesStorage.Wallet.CloseAsync();
                //if (holderWalletTwo != null) await holderWalletTwo.AriesStorage.Wallet.CloseAsync();

                //await Wallet.DeleteWalletAsync(_issuerConfig, Credentials);
                //await Wallet.DeleteWalletAsync(_holderConfig, Credentials);
                //await Wallet.DeleteWalletAsync(_holderConfigTwo, Credentials);
                await base.DisposeAsync();
            }
        }

        [Fact]
        public async Task CanCreateInvitationAsync()
        {
            var connectionId = Guid.NewGuid().ToString();

            var (msg, record) = await _connectionService.CreateInvitationAsync(_fixture.issuerWallet,
                new InviteConfiguration { ConnectionId = connectionId });

            var connection = await _connectionService.GetAsync(_fixture.issuerWallet, connectionId);

            Assert.False(connection.MultiPartyInvitation);
            Assert.Equal(ConnectionState.Invited, connection.State);
            Assert.Equal(connectionId, connection.Id);
        }

        [Fact]
        public async Task CanCreateInvitationWithDidKeyFormatAsync()
        {
            var connectionId = Guid.NewGuid().ToString();

            var (msg, record) = await _connectionService.CreateInvitationAsync(_fixture.issuerWallet,
                new InviteConfiguration { ConnectionId = connectionId, UseDidKeyFormat = true });

            var connection = await _connectionService.GetAsync(_fixture.issuerWallet, connectionId);

            Assert.True(DidUtils.IsDidKey(msg.RecipientKeys.First()));
            Assert.True(DidUtils.IsDidKey(msg.RoutingKeys.First()));
            Assert.False(connection.MultiPartyInvitation);
            Assert.Equal(ConnectionState.Invited, connection.State);
            Assert.Equal(connectionId, connection.Id);
        }

        [Fact]
        public async Task CanCreateMultiPartyInvitationAsync()
        {
            var connectionId = Guid.NewGuid().ToString();

            await _connectionService.CreateInvitationAsync(_fixture.issuerWallet,
                new InviteConfiguration { ConnectionId = connectionId, MultiPartyInvitation = true });

            var connection = await _connectionService.GetAsync(_fixture.issuerWallet, connectionId);

            Assert.True(connection.MultiPartyInvitation);
            Assert.Equal(ConnectionState.Invited, connection.State);
            Assert.Equal(connectionId, connection.Id);
        }

        [Fact]
        public async Task CreateInvitiationThrowsInvalidStateNoEndpoint()
        {
            _provisioningService = ServiceUtils.GetDefaultMockProvisioningService(null, "DefaultMasterSecret", null);

            _connectionService = new DefaultConnectionServiceV2(
                _eventAggregator,
                new DefaultWalletRecordServiceV2(),
                _provisioningService,
                new Mock<ILogger<DefaultConnectionServiceV2>>().Object);

            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _connectionService.CreateInvitationAsync(_fixture.issuerWallet,
                new InviteConfiguration()));

            Assert.True(ex.ErrorCode == ErrorCode.RecordInInvalidState);
        }

        [Fact]
        public async Task CanCreateRequestWithoutEndpoint()
        {
            var provisioningService = ServiceUtils.GetDefaultMockProvisioningService(null, "DefaultMasterSecret", null);

            var connectionService = new DefaultConnectionServiceV2(
                _eventAggregator,
                new DefaultWalletRecordServiceV2(),
                provisioningService,
                new Mock<ILogger<DefaultConnectionServiceV2>>().Object);

            var (invite, _) = await _connectionService.CreateInvitationAsync(_fixture.issuerWallet,
                new InviteConfiguration());

            var (request, _) = await connectionService.CreateRequestAsync(_fixture.holderWallet, invite);

            Assert.True(request.Connection.DidDoc.Services.Count == 0);
        }

        [Fact]
        public async Task CanReceiveRequestWithoutEndpoint()
        {
            var provisioningService = ServiceUtils.GetDefaultMockProvisioningService(null, "DefaultMasterSecret", null);

            var connectionService = new DefaultConnectionServiceV2(
                _eventAggregator,
                new DefaultWalletRecordServiceV2(),
                provisioningService,
                new Mock<ILogger<DefaultConnectionServiceV2>>().Object);

            var (invite, inviteeConnection) = await _connectionService.CreateInvitationAsync(_fixture.issuerWallet,
                new InviteConfiguration());

            var (request, _) = await connectionService.CreateRequestAsync(_fixture.holderWallet, invite);

            var id = await _connectionService.ProcessRequestAsync(_fixture.issuerWallet, request, inviteeConnection);

            inviteeConnection = await _connectionService.GetAsync(_fixture.issuerWallet, id);

            Assert.True(inviteeConnection.State == ConnectionState.Negotiating);
            Assert.True(request.Connection.DidDoc.Services.Count == 0);
        }

        [Fact]
        public async Task AcceptRequestThrowsExceptionConnectionNotFound()
        {
            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _connectionService.CreateResponseAsync(_fixture.issuerWallet, "bad-connection-id"));
            Assert.True(ex.ErrorCode == ErrorCode.RecordNotFound);
        }

        [Fact]
        public async Task AcceptRequestThrowsExceptionConnectionInvalidState()
        {
            var connectionId = Guid.NewGuid().ToString();

            await _connectionService.CreateInvitationAsync(_fixture.issuerWallet,
                new InviteConfiguration { ConnectionId = connectionId, AutoAcceptConnection = false });

            //Process a connection request
            var connectionRecord = await _connectionService.GetAsync(_fixture.issuerWallet, connectionId);

            await _connectionService.ProcessRequestAsync(_fixture.issuerWallet, new ConnectionRequestMessage
            {
                Connection = new Connection
                {
                    Did = "EYS94e95kf6LXF49eARL76",
                    DidDoc = new ConnectionRecord
                    {
                        MyVk = "6vyxuqpe3UBcTmhF3Wmmye2UVroa51Lcd9smQKFB5QX1"
                    }.MyDidDoc(await _provisioningService.GetProvisioningAsync(_fixture.issuerWallet.AriesStorage))
                }
            }, connectionRecord);

            //Accept the connection request
            await _connectionService.CreateResponseAsync(_fixture.issuerWallet, connectionId);

            //Now try and accept it again
            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _connectionService.CreateResponseAsync(_fixture.issuerWallet, connectionId));

            Assert.True(ex.ErrorCode == ErrorCode.RecordInInvalidState);
        }

        [Fact]
        public async Task RevokeInvitationThrowsConnectionNotFound()
        {
            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _connectionService.RevokeInvitationAsync(_fixture.issuerWallet, "bad-connection-id"));
            Assert.True(ex.ErrorCode == ErrorCode.RecordNotFound);
        }

        [Fact]
        public async Task RevokeInvitationThrowsConnectionInvalidState()
        {
            var connectionId = Guid.NewGuid().ToString();

            await _connectionService.CreateInvitationAsync(_fixture.issuerWallet,
                new InviteConfiguration { ConnectionId = connectionId, AutoAcceptConnection = false });

            //Process a connection request
            var connectionRecord = await _connectionService.GetAsync(_fixture.issuerWallet, connectionId);

            await _connectionService.ProcessRequestAsync(_fixture.issuerWallet, new ConnectionRequestMessage
            {
                Connection = new Connection
                {
                    Did = "EYS94e95kf6LXF49eARL76",
                    DidDoc = new ConnectionRecord
                    {
                        MyVk = "6vyxuqpe3UBcTmhF3Wmmye2UVroa51Lcd9smQKFB5QX1"
                    }.MyDidDoc(await _provisioningService.GetProvisioningAsync(_fixture.issuerWallet.AriesStorage))
                }
            }, connectionRecord);

            //Accept the connection request
            await _connectionService.CreateResponseAsync(_fixture.issuerWallet, connectionId);

            //Now try and revoke invitation
            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _connectionService.RevokeInvitationAsync(_fixture.issuerWallet, connectionId));

            Assert.True(ex.ErrorCode == ErrorCode.RecordInInvalidState);
        }

        [Fact]
        public async Task CanRevokeInvitation()
        {
            var connectionId = Guid.NewGuid().ToString();

            await _connectionService.CreateInvitationAsync(_fixture.issuerWallet,
                new InviteConfiguration { ConnectionId = connectionId });

            var connection = await _connectionService.GetAsync(_fixture.issuerWallet, connectionId);

            Assert.False(connection.MultiPartyInvitation);
            Assert.Equal(ConnectionState.Invited, connection.State);
            Assert.Equal(connectionId, connection.Id);

            await _connectionService.RevokeInvitationAsync(_fixture.issuerWallet, connectionId);

            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _connectionService.CreateResponseAsync(_fixture.issuerWallet, connectionId));
            
            Assert.True(ex.ErrorCode == ErrorCode.RecordNotFound);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CanEstablishConnectionAsync(bool useDidKeyFormat)
        {
            var events = 0;
            _eventAggregator.GetEventByType<ServiceMessageProcessingEvent>()
                .Where(_ => (_.MessageType == MessageTypes.ConnectionRequest ||
                             _.MessageType == MessageTypes.ConnectionResponse))
                .Subscribe(_ =>
                {
                    events++;
                });


            var (connectionIssuer, connectionHolder) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _fixture.issuerWallet, _fixture.holderWallet, useDidKeyFormat: useDidKeyFormat);

            Assert.True(events == 2);

            Assert.Equal(ConnectionState.Connected, connectionIssuer.State);
            Assert.Equal(ConnectionState.Connected, connectionHolder.State);

            Assert.Equal(connectionIssuer.MyDid, connectionHolder.TheirDid);
            Assert.Equal(connectionIssuer.TheirDid, connectionHolder.MyDid);

            Assert.Equal(connectionIssuer.Endpoint.Uri, TestConstants.DefaultMockUri);
            Assert.Equal(connectionIssuer.Endpoint.Uri, TestConstants.DefaultMockUri);
        }

        [Fact]
        public async Task CanEstablishConnectionsWithMultiPartyInvitationAsync()
        {
            (var invite, var record) = await _connectionService.CreateInvitationAsync(_fixture.issuerWallet,
                new InviteConfiguration { MultiPartyInvitation = true });

            var (connectionIssuer, connectionHolderOne) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _fixture.issuerWallet, _fixture.holderWallet, invite, record.Id);

            _messages.Clear();

            var (connectionIssuerTwo, connectionHolderTwo) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _fixture.issuerWallet, _fixture.holderWalletTwo, invite, record.Id);

            Assert.Equal(ConnectionState.Connected, connectionIssuer.State);
            Assert.Equal(ConnectionState.Connected, connectionHolderOne.State);

            Assert.Equal(ConnectionState.Connected, connectionIssuerTwo.State);
            Assert.Equal(ConnectionState.Connected, connectionHolderTwo.State);

            Assert.Equal(connectionIssuer.MyDid, connectionHolderOne.TheirDid);
            Assert.Equal(connectionIssuer.TheirDid, connectionHolderOne.MyDid);

            Assert.Equal(connectionIssuerTwo.MyDid, connectionHolderTwo.TheirDid);
            Assert.Equal(connectionIssuerTwo.TheirDid, connectionHolderTwo.MyDid);

            Assert.Equal(connectionIssuer.Endpoint.Uri, TestConstants.DefaultMockUri);
            Assert.Equal(connectionIssuerTwo.Endpoint.Uri, TestConstants.DefaultMockUri);
        }
    }
}
