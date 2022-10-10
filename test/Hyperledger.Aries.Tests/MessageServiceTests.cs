using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.Handshakes.Common;
using Hyperledger.Aries.Features.Handshakes.Connection;
using Hyperledger.Aries.Features.Handshakes.Connection.Models;
using Hyperledger.Aries.Features.Routing;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Aries.Utils;
using Hyperledger.Indy.DidApi;
using Hyperledger.Indy.WalletApi;
using Hyperledger.TestHarness;
using Hyperledger.TestHarness.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Hyperledger.Aries.Tests
{
    public class MockAgentMessage : AgentMessage { }

    [Trait("Category", "DefaultV1")]
    public class MessageServiceTestsV1 : IAsyncLifetime
    {
        private string Config = "{\"id\":\"" + Guid.NewGuid() + "\"}";
        private const string WalletCredentials = "{\"key\":\"test_wallet_key\"}";

        private Wallet _wallet;

        private readonly IMessageService _messagingService;

        private readonly ConcurrentBag<HttpRequestMessage> _messages = new ConcurrentBag<HttpRequestMessage>();

        public MessageServiceTestsV1()
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                // Setup the PROTECTED method to mock
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                // prepare the expected response of the mocked http call
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(""),
                })
                .Callback((HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    _messages.Add(request);
                })
                .Verifiable();

            var clientFactory = new Mock<IHttpClientFactory>();
            clientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(handlerMock.Object));

            var mockConnectionService = new Mock<IConnectionService>();
            mockConnectionService.Setup(_ => _.ListAsync(It.IsAny<IAgentContext>(), It.IsAny<ISearchQuery>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.FromResult(new List<ConnectionRecord> { new ConnectionRecord() }));

            var httpMessageDispatcher = new HttpMessageDispatcher(clientFactory.Object);

            _messagingService =
                new DefaultMessageService(new Mock<ILogger<DefaultMessageService>>().Object, new[] { httpMessageDispatcher }, recordService: null);
        }

        public async Task InitializeAsync()
        {
            try
            {
                await Wallet.CreateWalletAsync(Config, WalletCredentials);
            }
            catch (WalletExistsException)
            {
                // OK
            }

            _wallet = await Wallet.OpenWalletAsync(Config, WalletCredentials);
        }

        public async Task DisposeAsync()
        {
            if (_wallet != null) await _wallet.CloseAsync();
            await Wallet.DeleteWalletAsync(Config, WalletCredentials);
        }

        [Fact]
        public async Task PackAnon()
        {

            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } }.ToByteArray();

            var my = await Did.CreateAndStoreMyDidAsync(_wallet, "{}");
            var anotherMy = await Did.CreateAndStoreMyDidAsync(_wallet, "{}");

            var packed = await CryptoUtils.PackAsync(_wallet, anotherMy.VerKey, message, null);

            Assert.NotNull(packed);
        }

        [Fact]
        public async Task PackAuth()
        {

            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } }.ToByteArray();

            var my = await Did.CreateAndStoreMyDidAsync(_wallet, "{}");
            var anotherMy = await Did.CreateAndStoreMyDidAsync(_wallet, "{}");

            var packed = await CryptoUtils.PackAsync(_wallet, anotherMy.VerKey, message, my.VerKey);

            Assert.NotNull(packed);
        }

        [Fact]
        public async Task PackAndUnpackAnon()
        {

            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } };

            var my = await Did.CreateAndStoreMyDidAsync(_wallet, "{}");
            var anotherMy = await Did.CreateAndStoreMyDidAsync(_wallet, "{}");

            var packed = await CryptoUtils.PackAsync(_wallet, anotherMy.VerKey, message, null);
            var unpack = await CryptoUtils.UnpackAsync(_wallet, packed);

            Assert.NotNull(unpack);
            Assert.Null(unpack.SenderVerkey);
            Assert.NotNull(unpack.RecipientVerkey);
            Assert.Equal(unpack.RecipientVerkey, anotherMy.VerKey);
        }

        [Fact]
        public async Task PackAndUnpackAuth()
        {

            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } }.ToByteArray();

            var my = await Did.CreateAndStoreMyDidAsync(_wallet, "{}");
            var anotherMy = await Did.CreateAndStoreMyDidAsync(_wallet, "{}");

            var packed = await CryptoUtils.PackAsync(_wallet, anotherMy.VerKey, message, my.VerKey);
            var unpack = await CryptoUtils.UnpackAsync(_wallet, packed);

            var jObject = JObject.Parse(unpack.Message);

            Assert.NotNull(unpack);
            Assert.NotNull(unpack.SenderVerkey);
            Assert.NotNull(unpack.RecipientVerkey);
            Assert.Equal(unpack.RecipientVerkey, anotherMy.VerKey);
            Assert.Equal(unpack.SenderVerkey, my.VerKey);
            Assert.Equal(MessageTypes.ConnectionInvitation, jObject["@type"].ToObject<string>());
        }

        [Fact]
        public async Task UnpackToCustomType()
        {

            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } };

            var my = await Did.CreateAndStoreMyDidAsync(_wallet, "{}");
            var anotherMy = await Did.CreateAndStoreMyDidAsync(_wallet, "{}");

            var packed = await CryptoUtils.PackAsync(_wallet, anotherMy.VerKey, message.ToByteArray(), null);
            var unpack = await CryptoUtils.UnpackAsync<ConnectionInvitationMessage>(_wallet, packed);

            Assert.NotNull(unpack);
            Assert.Equal("123", unpack.RecipientKeys[0]);
        }

        [Fact]
        public async Task AuthPrepareMessageNoRoutingAsync()
        {
            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } };

            var recipient = await Did.CreateAndStoreMyDidAsync(_wallet, "{}");
            var sender = await Did.CreateAndStoreMyDidAsync(_wallet, "{}");

            var agentContext = new AgentContext() { AriesStorage = new AriesStorage(wallet: _wallet) };
            var encrypted = await CryptoUtils.PrepareAsync(agentContext, message, recipient.VerKey, new string[0], sender.VerKey);

            var unpackRes = await CryptoUtils.UnpackAsync(agentContext.AriesStorage.Wallet, encrypted);
            var unpackMsg = JsonConvert.DeserializeObject<ConnectionInvitationMessage>(unpackRes.Message);

            Assert.NotNull(unpackMsg);
            Assert.True(unpackRes.SenderVerkey == sender.VerKey);
            Assert.True(unpackRes.RecipientVerkey == recipient.VerKey);
            Assert.Equal("123", unpackMsg.RecipientKeys[0]);
        }

        [Fact]
        public async Task AuthPrepareMessageRoutingAsync()
        {
            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } };

            var recipient = await Did.CreateAndStoreMyDidAsync(_wallet, "{}");
            var sender = await Did.CreateAndStoreMyDidAsync(_wallet, "{}");
            var routingRecipient = await Did.CreateAndStoreMyDidAsync(_wallet, "{}");

            var agentContext = new AgentContext() { AriesStorage = new AriesStorage(wallet: _wallet) };
            var encrypted = await CryptoUtils.PrepareAsync(agentContext, message, recipient.VerKey, new[] { routingRecipient.VerKey }, sender.VerKey);

            var unpackRes = await CryptoUtils.UnpackAsync(_wallet, encrypted);
            var unpackMsg = JsonConvert.DeserializeObject<ForwardMessage>(unpackRes.Message);

            Assert.NotNull(unpackMsg);
            Assert.True(string.IsNullOrEmpty(unpackRes.SenderVerkey));
            Assert.True(unpackRes.RecipientVerkey == routingRecipient.VerKey);
            Assert.Equal(recipient.VerKey, unpackMsg.To);

            var unpackRes1 = await CryptoUtils.UnpackAsync(_wallet, unpackMsg.Message.ToJson().GetUTF8Bytes());
            var unpackMsg1 = JsonConvert.DeserializeObject<ConnectionInvitationMessage>(unpackRes1.Message);

            Assert.NotNull(unpackMsg1);
            Assert.True(unpackRes1.SenderVerkey == sender.VerKey);
            Assert.True(unpackRes1.RecipientVerkey == recipient.VerKey);
            Assert.Equal("123", unpackMsg1.RecipientKeys[0]);
        }

        [Fact]
        public async Task AnonPrepareMessageNoRoutingAsync()
        {
            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } };

            var recipient = await Did.CreateAndStoreMyDidAsync(_wallet, "{}");

            var agentContext = new AgentContext() { AriesStorage = new AriesStorage(wallet: _wallet) };
            var encrypted = await CryptoUtils.PrepareAsync(agentContext, message, recipient.VerKey);

            var unpackRes = await CryptoUtils.UnpackAsync(_wallet, encrypted);
            var unpackMsg = JsonConvert.DeserializeObject<ConnectionInvitationMessage>(unpackRes.Message);

            Assert.NotNull(unpackMsg);
            Assert.True(string.IsNullOrEmpty(unpackRes.SenderVerkey));
            Assert.True(unpackRes.RecipientVerkey == recipient.VerKey);
            Assert.Equal("123", unpackMsg.RecipientKeys[0]);
        }

        [Fact(DisplayName = "Forward message to recipient with multiple routing keys")]
        public async Task PrepareMessageForMultipleRoutingHops()
        {
            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } };

            var json = message.ToJson();
            var messageBack = json.ToObject<ConnectionInvitationMessage>();

            var keys = new
            {
                Sender = await Did.CreateAndStoreMyDidAsync(_wallet, "{}"),
                Recipient = await Did.CreateAndStoreMyDidAsync(_wallet, "{}"),
                RoutingOne = await Did.CreateAndStoreMyDidAsync(_wallet, "{}"),
                RoutingTwo = await Did.CreateAndStoreMyDidAsync(_wallet, "{}")
            };

            var agentContext = new AgentContext() { AriesStorage = new AriesStorage(wallet: _wallet) };
            // Prepare the message for transport by packing the agent message with multiple forward messages
            var transportMessage = await CryptoUtils.PrepareAsync(
                agentContext: agentContext,
                message: message,
                recipientKey: keys.Recipient.VerKey,
                routingKeys: new[] { keys.RoutingOne.VerKey, keys.RoutingTwo.VerKey },
                senderKey: keys.Sender.VerKey);

            // Unpack and assert outter forward message
            var outterResult = await CryptoUtils.UnpackAsync(_wallet, transportMessage);
            var outterMessage = outterResult.Message.ToObject<ForwardMessage>();

            Assert.Equal(keys.RoutingTwo.VerKey, outterResult.RecipientVerkey);
            Assert.Null(outterResult.SenderVerkey);
            Assert.NotNull(outterMessage);
            Assert.Equal(outterMessage.To, keys.RoutingOne.VerKey);

            // Unpack and test inner forward message
            var innerResult = await CryptoUtils.UnpackAsync(_wallet, outterMessage.Message.ToJson().GetUTF8Bytes());
            var innerMessage = innerResult.Message.ToObject<ForwardMessage>();

            Assert.Equal(keys.RoutingOne.VerKey, innerResult.RecipientVerkey);
            Assert.Null(innerResult.SenderVerkey);
            Assert.NotNull(innerMessage);
            Assert.Equal(innerMessage.To, keys.Recipient.VerKey);

            // Unpack and test inner content message
            var contentResult = await CryptoUtils.UnpackAsync(_wallet, innerMessage.Message.ToJson().GetUTF8Bytes());
            var contentMessage = contentResult.Message.ToObject<ConnectionInvitationMessage>();

            Assert.Equal(keys.Recipient.VerKey, contentResult.RecipientVerkey);
            Assert.Equal(keys.Sender.VerKey, contentResult.SenderVerkey);
            Assert.NotNull(contentMessage);
            Assert.NotEmpty(contentMessage.RecipientKeys);
            Assert.Single(contentMessage.RecipientKeys);
            Assert.Equal("123", contentMessage.RecipientKeys.First());
        }

        [Fact]
        public async Task AnonPrepareMessageRoutingAsync()
        {
            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } };

            var recipient = await Did.CreateAndStoreMyDidAsync(_wallet, "{}");
            var routingRecipient = await Did.CreateAndStoreMyDidAsync(_wallet, "{}");

            var agentContext = new AgentContext() { AriesStorage = new AriesStorage(wallet: _wallet) };
            var encrypted = await CryptoUtils.PrepareAsync(agentContext, message, recipient.VerKey, new[] { routingRecipient.VerKey });

            var unpackRes = await CryptoUtils.UnpackAsync(_wallet, encrypted);
            var unpackMsg = JsonConvert.DeserializeObject<ForwardMessage>(unpackRes.Message);

            Assert.NotNull(unpackMsg);
            Assert.True(string.IsNullOrEmpty(unpackRes.SenderVerkey));
            Assert.True(unpackRes.RecipientVerkey == routingRecipient.VerKey);
            Assert.Equal(recipient.VerKey, unpackMsg.To);

            var unpackRes1 = await CryptoUtils.UnpackAsync(_wallet, unpackMsg.Message.ToJson().GetUTF8Bytes());
            var unpackMsg1 = JsonConvert.DeserializeObject<ConnectionInvitationMessage>(unpackRes1.Message);

            Assert.NotNull(unpackMsg1);
            Assert.True(string.IsNullOrEmpty(unpackRes1.SenderVerkey));
            Assert.True(unpackRes1.RecipientVerkey == recipient.VerKey);
            Assert.Equal("123", unpackMsg1.RecipientKeys[0]);
        }

        [Fact]
        public async Task SendToConnectionAsyncThrowsInvalidMessageNoId()
        {
            var connection = new ConnectionRecord
            {
                Alias = new ConnectionAlias
                {
                    Name = "Test"
                },
                Endpoint = new AgentEndpoint
                {
                    Uri = "https://mock.com"
                },
                TheirVk = Guid.NewGuid().ToString()
            };

            var agentContext = new AgentContext() { AriesStorage = new AriesStorage(wallet: _wallet) };
            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () =>
                await _messagingService.SendAsync(agentContext, new MockAgentMessage(), connection));
            Assert.True(ex.ErrorCode == ErrorCode.InvalidMessage);
        }

        [Fact]
        public async Task SendToConnectionAsyncThrowsInvalidMessageNoType()
        {
            var connection = new ConnectionRecord
            {
                Alias = new ConnectionAlias
                {
                    Name = "Test"
                },
                Endpoint = new AgentEndpoint
                {
                    Uri = "https://mock.com"
                },
                TheirVk = Guid.NewGuid().ToString()
            };

            var agentContext = new AgentContext() { AriesStorage = new AriesStorage(wallet: _wallet) };
            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () =>
                await _messagingService.SendAsync(agentContext, new MockAgentMessage { Id = Guid.NewGuid().ToString() }, connection));
            Assert.True(ex.ErrorCode == ErrorCode.InvalidMessage);
        }
    }

    [Trait("Category", "DefaultV2")]
    public class MessageServiceTestsV2 : IAsyncLifetime
    {
        private readonly WalletConfiguration _walletConfig = TestConstants.TestSingleWalletV2WalletConfig;
        private readonly WalletCredentials _walletCredentials = TestConstants.TestSingelWalletV2WalletCreds;

        private AriesStorage _storage;

        private  IMessageService _messagingService;
        private  IWalletService _walletService;
        private  IWalletRecordService _walletRecordServiceV2;

        private readonly ConcurrentBag<HttpRequestMessage> _messages = new ConcurrentBag<HttpRequestMessage>();

        public MessageServiceTestsV2()
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                // Setup the PROTECTED method to mock
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                // prepare the expected response of the mocked http call
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(""),
                })
                .Callback((HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    _messages.Add(request);
                })
                .Verifiable();

            var clientFactory = new Mock<IHttpClientFactory>();
            clientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(handlerMock.Object));

            var mockConnectionService = new Mock<IConnectionService>();
            mockConnectionService.Setup(_ => _.ListAsync(It.IsAny<IAgentContext>(), It.IsAny<ISearchQuery>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.FromResult(new List<ConnectionRecord> { new ConnectionRecord() }));

            var httpMessageDispatcher = new HttpMessageDispatcher(clientFactory.Object);

            _messagingService =
                new DefaultMessageService(new Mock<ILogger<DefaultMessageService>>().Object, new[] { httpMessageDispatcher }, _walletRecordServiceV2);           

            _walletService = new DefaultWalletServiceV2();
            _walletRecordServiceV2 = new DefaultWalletRecordServiceV2();
        }

        public async Task InitializeAsync()
        {
            var agentContext = await AgentUtils.CreateV2(_walletService, _walletConfig, _walletCredentials);
            _storage = agentContext.AriesStorage;
        }

        [Fact]
        public async Task PackAnon()
        {

            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } }.ToByteArray();

            (_, string verKey )= await DidUtils.CreateAndStoreMyDidAsync(_storage, _walletRecordServiceV2);

            var packed = await CryptoUtils.PackAsync(_storage, verKey, message, null, _walletRecordServiceV2);

            Assert.NotNull(packed);
        }

        [Fact]
        public async Task PackAuth()
        {

            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } }.ToByteArray();

            (_, string myVerKey) = await DidUtils.CreateAndStoreMyDidAsync(_storage, _walletRecordServiceV2);
            (_, string anotherVerKey) = await DidUtils.CreateAndStoreMyDidAsync(_storage, _walletRecordServiceV2);

            var packed = await CryptoUtils.PackAsync(_storage, anotherVerKey, message, myVerKey, _walletRecordServiceV2);

            Assert.NotNull(packed);
        }

        [Fact]
        public async Task PackAndUnpackAnon()
        {

            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } }.ToByteArray();

            (_, string verKey) = await DidUtils.CreateAndStoreMyDidAsync(_storage, _walletRecordServiceV2);

            var packed = await CryptoUtils.PackAsync(_storage, verKey, message, null, _walletRecordServiceV2);
            var unpack = await CryptoUtils.UnpackAsync(_storage, packed, _walletRecordServiceV2);

            Assert.NotNull(unpack);
            Assert.Null(unpack.SenderVerkey);
            Assert.NotNull(unpack.RecipientVerkey);
            Assert.Equal(unpack.RecipientVerkey, verKey);
        }

        [Fact]
        public async Task PackAndUnpackAuth()
        {

            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } }.ToByteArray();

            (_, string myVerKey) = await DidUtils.CreateAndStoreMyDidAsync(_storage, _walletRecordServiceV2);
            (_, string anotherVerKey) = await DidUtils.CreateAndStoreMyDidAsync(_storage, _walletRecordServiceV2);

            var packed = await CryptoUtils.PackAsync(_storage, anotherVerKey, message, myVerKey, _walletRecordServiceV2);
            var unpack = await CryptoUtils.UnpackAsync(_storage, packed, _walletRecordServiceV2);

            var jObject = JObject.Parse(unpack.Message);

            Assert.NotNull(unpack);
            Assert.NotNull(unpack.SenderVerkey);
            Assert.NotNull(unpack.RecipientVerkey);
            Assert.Equal(unpack.RecipientVerkey, anotherVerKey);
            Assert.Equal(unpack.SenderVerkey, myVerKey);
            Assert.Equal(MessageTypes.ConnectionInvitation, jObject["@type"].ToObject<string>());
        }

        [Fact]
        public async Task UnpackToCustomType()
        {

            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } };

            (_, string myVerKey) = await DidUtils.CreateAndStoreMyDidAsync(_storage, _walletRecordServiceV2);
            (_, string anotherVerKey) = await DidUtils.CreateAndStoreMyDidAsync(_storage, _walletRecordServiceV2);

            var packed = await CryptoUtils.PackAsync(_storage, anotherVerKey, message.ToByteArray(), null, _walletRecordServiceV2);
            var unpack = await CryptoUtils.UnpackAsync<ConnectionInvitationMessage>(_storage, packed, _walletRecordServiceV2);

            Assert.NotNull(unpack);
            Assert.Equal("123", unpack.RecipientKeys[0]);
        }

        [Fact]
        public async Task AuthPrepareMessageNoRoutingAsync()
        {
            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } };

            (_, string recipientVerKey) = await DidUtils.CreateAndStoreMyDidAsync(_storage, _walletRecordServiceV2);
            (_, string senderVerKey) = await DidUtils.CreateAndStoreMyDidAsync(_storage, _walletRecordServiceV2);

            var agentContext = new AgentContext() { AriesStorage = _storage };
            var encrypted = await CryptoUtils.PrepareAsync(agentContext, message, recipientVerKey, new string[0], senderVerKey, _walletRecordServiceV2);

            var unpackRes = await CryptoUtils.UnpackAsync(agentContext.AriesStorage, encrypted, _walletRecordServiceV2);
            var unpackMsg = JsonConvert.DeserializeObject<ConnectionInvitationMessage>(unpackRes.Message);

            Assert.NotNull(unpackMsg);
            Assert.True(unpackRes.SenderVerkey == senderVerKey);
            Assert.True(unpackRes.RecipientVerkey == recipientVerKey);
            Assert.Equal("123", unpackMsg.RecipientKeys[0]);
        }

        [Fact]
        public async Task AuthPrepareMessageRoutingAsync()
        {
            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } };

            (_, string recipientVerKey) = await DidUtils.CreateAndStoreMyDidAsync(_storage, _walletRecordServiceV2);
            (_, string senderVerKey) = await DidUtils.CreateAndStoreMyDidAsync(_storage, _walletRecordServiceV2);
            (_, string routingRecipientVerKey) = await DidUtils.CreateAndStoreMyDidAsync(_storage, _walletRecordServiceV2);

            var agentContext = new AgentContext() { AriesStorage = _storage };
            var encrypted = await CryptoUtils.PrepareAsync(agentContext, message, recipientVerKey, new[] { routingRecipientVerKey }, senderVerKey, _walletRecordServiceV2);

            var unpackRes = await CryptoUtils.UnpackAsync(_storage, encrypted, _walletRecordServiceV2);
            var unpackMsg = JsonConvert.DeserializeObject<ForwardMessage>(unpackRes.Message);

            Assert.NotNull(unpackMsg);
            Assert.True(string.IsNullOrEmpty(unpackRes.SenderVerkey));
            Assert.True(unpackRes.RecipientVerkey == routingRecipientVerKey);
            Assert.Equal(recipientVerKey, unpackMsg.To);

            var unpackRes1 = await CryptoUtils.UnpackAsync(_storage, unpackMsg.Message.ToJson().GetUTF8Bytes(), _walletRecordServiceV2);
            var unpackMsg1 = JsonConvert.DeserializeObject<ConnectionInvitationMessage>(unpackRes1.Message);

            Assert.NotNull(unpackMsg1);
            Assert.True(unpackRes1.SenderVerkey == senderVerKey);
            Assert.True(unpackRes1.RecipientVerkey == recipientVerKey);
            Assert.Equal("123", unpackMsg1.RecipientKeys[0]);
        }

        [Fact]
        public async Task AnonPrepareMessageNoRoutingAsync()
        {
            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } };

            (_, string recipientVerKey) = await DidUtils.CreateAndStoreMyDidAsync(_storage, _walletRecordServiceV2);

            var agentContext = new AgentContext() { AriesStorage = _storage };
            var encrypted = await CryptoUtils.PrepareAsync(agentContext, message, recipientVerKey, recordService: _walletRecordServiceV2);

            var unpackRes = await CryptoUtils.UnpackAsync(_storage, encrypted, _walletRecordServiceV2);
            var unpackMsg = JsonConvert.DeserializeObject<ConnectionInvitationMessage>(unpackRes.Message);

            Assert.NotNull(unpackMsg);
            Assert.True(string.IsNullOrEmpty(unpackRes.SenderVerkey));
            Assert.True(unpackRes.RecipientVerkey == recipientVerKey);
            Assert.Equal("123", unpackMsg.RecipientKeys[0]);
        }

        [Fact(DisplayName = "Forward message to recipient with multiple routing keys")]
        public async Task PrepareMessageForMultipleRoutingHops()
        {
            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } };

            var json = message.ToJson();
            var messageBack = json.ToObject<ConnectionInvitationMessage>();

            (_, string sender) = await DidUtils.CreateAndStoreMyDidAsync(_storage, _walletRecordServiceV2);
            (_, string recipient) = await DidUtils.CreateAndStoreMyDidAsync(_storage, _walletRecordServiceV2);
            (_, string routingOne) = await DidUtils.CreateAndStoreMyDidAsync(_storage, _walletRecordServiceV2);
            (_, string routingTwo) = await DidUtils.CreateAndStoreMyDidAsync(_storage, _walletRecordServiceV2);

            var keys = new
            {
                SenderVerKey = sender,
                RecipientVerKey = recipient,
                RoutingOneVerKey = routingOne,
                RoutingTwoVerKey = routingTwo
            };

            var agentContext = new AgentContext() { AriesStorage = _storage };
            // Prepare the message for transport by packing the agent message with multiple forward messages
            var transportMessage = await CryptoUtils.PrepareAsync(
                agentContext: agentContext,
                message: message,
                recipientKey: keys.RecipientVerKey,
                routingKeys: new[] { keys.RoutingOneVerKey, keys.RoutingTwoVerKey },
                senderKey: keys.SenderVerKey,
                recordService: _walletRecordServiceV2);

            // Unpack and assert outter forward message
            var outterResult = await CryptoUtils.UnpackAsync(_storage, transportMessage, _walletRecordServiceV2);
            var outterMessage = outterResult.Message.ToObject<ForwardMessage>();

            Assert.Equal(keys.RoutingTwoVerKey, outterResult.RecipientVerkey);
            Assert.Null(outterResult.SenderVerkey);
            Assert.NotNull(outterMessage);
            Assert.Equal(outterMessage.To, keys.RoutingOneVerKey);

            // Unpack and test inner forward message
            var innerResult = await CryptoUtils.UnpackAsync(_storage, outterMessage.Message.ToJson().GetUTF8Bytes(), _walletRecordServiceV2);
            var innerMessage = innerResult.Message.ToObject<ForwardMessage>();

            Assert.Equal(keys.RoutingOneVerKey, innerResult.RecipientVerkey);
            Assert.Null(innerResult.SenderVerkey);
            Assert.NotNull(innerMessage);
            Assert.Equal(innerMessage.To, keys.RecipientVerKey);

            // Unpack and test inner content message
            var contentResult = await CryptoUtils.UnpackAsync(_storage, innerMessage.Message.ToJson().GetUTF8Bytes(), _walletRecordServiceV2);
            var contentMessage = contentResult.Message.ToObject<ConnectionInvitationMessage>();

            Assert.Equal(keys.RecipientVerKey, contentResult.RecipientVerkey);
            Assert.Equal(keys.SenderVerKey, contentResult.SenderVerkey);
            Assert.NotNull(contentMessage);
            Assert.NotEmpty(contentMessage.RecipientKeys);
            Assert.Single(contentMessage.RecipientKeys);
            Assert.Equal("123", contentMessage.RecipientKeys.First());
        }

        [Fact]
        public async Task AnonPrepareMessageRoutingAsync()
        {
            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } };

            (_, string recipientVerKey) = await DidUtils.CreateAndStoreMyDidAsync(_storage, _walletRecordServiceV2);
            (_, string routingRecipientVerKey) = await DidUtils.CreateAndStoreMyDidAsync(_storage, _walletRecordServiceV2);

            var agentContext = new AgentContext() { AriesStorage = _storage };
            var encrypted = await CryptoUtils.PrepareAsync(agentContext, message, recipientVerKey, new[] { routingRecipientVerKey });

            var unpackRes = await CryptoUtils.UnpackAsync(_storage, encrypted, _walletRecordServiceV2);
            var unpackMsg = JsonConvert.DeserializeObject<ForwardMessage>(unpackRes.Message);

            Assert.NotNull(unpackMsg);
            Assert.True(string.IsNullOrEmpty(unpackRes.SenderVerkey));
            Assert.True(unpackRes.RecipientVerkey == routingRecipientVerKey);
            Assert.Equal(recipientVerKey, unpackMsg.To);

            var unpackRes1 = await CryptoUtils.UnpackAsync(_storage, unpackMsg.Message.ToJson().GetUTF8Bytes(), _walletRecordServiceV2);
            var unpackMsg1 = JsonConvert.DeserializeObject<ConnectionInvitationMessage>(unpackRes1.Message);

            Assert.NotNull(unpackMsg1);
            Assert.True(string.IsNullOrEmpty(unpackRes1.SenderVerkey));
            Assert.True(unpackRes1.RecipientVerkey == recipientVerKey);
            Assert.Equal("123", unpackMsg1.RecipientKeys[0]);
        }

        [Fact]
        public async Task SendToConnectionAsyncThrowsInvalidMessageNoId()
        {
            var connection = new ConnectionRecord
            {
                Alias = new ConnectionAlias
                {
                    Name = "Test"
                },
                Endpoint = new AgentEndpoint
                {
                    Uri = "https://mock.com"
                },
                TheirVk = Guid.NewGuid().ToString()
            };

            var agentContext = new AgentContext() { AriesStorage = _storage };
            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () =>
                await _messagingService.SendAsync(agentContext, new MockAgentMessage(), connection));
            Assert.True(ex.ErrorCode == ErrorCode.InvalidMessage);
        }

        [Fact]
        public async Task SendToConnectionAsyncThrowsInvalidMessageNoType()
        {
            var connection = new ConnectionRecord
            {
                Alias = new ConnectionAlias
                {
                    Name = "Test"
                },
                Endpoint = new AgentEndpoint
                {
                    Uri = "https://mock.com"
                },
                TheirVk = Guid.NewGuid().ToString()
            };

            var agentContext = new AgentContext() { AriesStorage = _storage };
            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () =>
                await _messagingService.SendAsync(agentContext, new MockAgentMessage { Id = Guid.NewGuid().ToString() }, connection));
            Assert.True(ex.ErrorCode == ErrorCode.InvalidMessage);
        }

        public async Task DisposeAsync()
        {
            await _walletService.DeleteWalletAsync(_walletConfig, _walletCredentials);
        }        
    }

    public class MessageServiceTestsV1V2 : IAsyncLifetime
    {
        private string Config = "{\"id\":\"" + Guid.NewGuid() + "\"}";
        private const string WalletCredentials = "{\"key\":\"test_wallet_key\"}";

        private readonly WalletConfiguration _walletConfig = TestConstants.TestSingleWalletV2WalletConfig;
        private readonly WalletCredentials _walletCredentials = TestConstants.TestSingelWalletV2WalletCreds;

        private AriesStorage _wallet;
        private AriesStorage _store;

        private readonly IMessageService _messagingService;
        private DefaultWalletServiceV2 _walletServiceV2;
        private DefaultWalletRecordServiceV2 _walletRecordServiceV2;

        private readonly ConcurrentBag<HttpRequestMessage> _messages = new ConcurrentBag<HttpRequestMessage>();

        public MessageServiceTestsV1V2()
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                // Setup the PROTECTED method to mock
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                // prepare the expected response of the mocked http call
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(""),
                })
                .Callback((HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    _messages.Add(request);
                })
                .Verifiable();

            var clientFactory = new Mock<IHttpClientFactory>();
            clientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(handlerMock.Object));

            var mockConnectionService = new Mock<IConnectionService>();
            mockConnectionService.Setup(_ => _.ListAsync(It.IsAny<IAgentContext>(), It.IsAny<ISearchQuery>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.FromResult(new List<ConnectionRecord> { new ConnectionRecord() }));

            var httpMessageDispatcher = new HttpMessageDispatcher(clientFactory.Object);

            _messagingService =
                new DefaultMessageService(new Mock<ILogger<DefaultMessageService>>().Object, new[] { httpMessageDispatcher }, recordService: null);

            _walletServiceV2 = new DefaultWalletServiceV2();
            _walletRecordServiceV2 = new DefaultWalletRecordServiceV2();
        }

        public async Task InitializeAsync()
        {
            var agentContextV1 = await AgentUtils.Create(Config, WalletCredentials);
            _wallet = agentContextV1.AriesStorage;

            var agentContextV2 = await AgentUtils.CreateV2(_walletServiceV2, _walletConfig, _walletCredentials);
            _store = agentContextV2.AriesStorage;
        }

        public async Task DisposeAsync()
        {
            await _walletServiceV2.DeleteWalletAsync(_walletConfig, _walletCredentials);

            if (_wallet.Wallet != null) await _wallet.Wallet.CloseAsync();
            await Wallet.DeleteWalletAsync(Config, WalletCredentials);
        }

        [Fact]
        public async Task PackV1AndUnpackV2Anon()
        {
            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } };

            //Simulate that there is already a connection and each one knows the verkey of the other party. In this case for simplicity myVerkey = theirVerkey
            var theirRecipient = await Did.CreateAndStoreMyDidAsync(_wallet.Wallet, new { seed = TestConstants.RecipientSeed}.ToJson());
            _ = await DidUtils.CreateAndStoreMyDidAsync(_store, _walletRecordServiceV2, seed : TestConstants.RecipientSeed);

            var packed = await CryptoUtils.PackAsync(_wallet, theirRecipient.VerKey, message, senderKey: null, recordService: null);
            var unpack = await CryptoUtils.UnpackAsync(_store, packed, recordService: _walletRecordServiceV2);

            Assert.NotNull(unpack);
            Assert.Null(unpack.SenderVerkey);
            Assert.NotNull(unpack.RecipientVerkey);
            Assert.Equal(unpack.RecipientVerkey, theirRecipient.VerKey);
        }

        [Fact]
        public async Task PackV1AndUnpackV2Auth()
        {
            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } }.ToByteArray();

            //Simulate that there is already a connection and each one knows the verkey of the other party. In this case for simplicity myVerkey = theirVerkey
            var mySender = await Did.CreateAndStoreMyDidAsync(_wallet.Wallet, new { seed = TestConstants.SenderSeed }.ToJson());
            _ = await DidUtils.CreateAndStoreMyDidAsync(_store, _walletRecordServiceV2, seed: TestConstants.SenderSeed);
            var theirRecipient = await Did.CreateAndStoreMyDidAsync(_wallet.Wallet, new { seed = TestConstants.RecipientSeed }.ToJson());
            _ = await DidUtils.CreateAndStoreMyDidAsync(_store, _walletRecordServiceV2, seed: TestConstants.RecipientSeed);

            var packed = await CryptoUtils.PackAsync(_wallet, theirRecipient.VerKey, message, mySender.VerKey, recordService: null);
            var unpack = await CryptoUtils.UnpackAsync(_store, packed, recordService: _walletRecordServiceV2);

            var jObject = JObject.Parse(unpack.Message);

            Assert.NotNull(unpack);
            Assert.NotNull(unpack.SenderVerkey);
            Assert.NotNull(unpack.RecipientVerkey);
            Assert.Equal(unpack.RecipientVerkey, theirRecipient.VerKey);
            Assert.Equal(unpack.SenderVerkey, mySender.VerKey);
            Assert.Equal(MessageTypes.ConnectionInvitation, jObject["@type"].ToObject<string>());
        }

        [Fact]
        public async Task PackV2AndUnpackV1Anon()
        {
            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } };

            //Simulate that there is already a connection and each one knows the verkey of the other party. In this case for simplicity myVerkey = theirVerkey
            var (_, theirRecipientVerkey) = await DidUtils.CreateAndStoreMyDidAsync(_store, _walletRecordServiceV2, seed: TestConstants.RecipientSeed);
            _ = await Did.CreateAndStoreMyDidAsync(_wallet.Wallet, new { seed = TestConstants.RecipientSeed }.ToJson());

            var packed = await CryptoUtils.PackAsync(_store, theirRecipientVerkey, message, senderKey: null, recordService: _walletRecordServiceV2);
            var unpack = await CryptoUtils.UnpackAsync(_wallet, packed, recordService: null);

            Assert.NotNull(unpack);
            Assert.Null(unpack.SenderVerkey);
            Assert.NotNull(unpack.RecipientVerkey);
            Assert.Equal(unpack.RecipientVerkey, theirRecipientVerkey);
        }

        [Fact]
        public async Task PackV2AndUnpackV1Auth()
        {

            var message = new ConnectionInvitationMessage { RecipientKeys = new[] { "123" } }.ToByteArray();

            //Simulate that there is already a connection and each one knows the verkey of the other party. In this case for simplicity myVerkey = theirVerkey
            var (_, mySenderVerkey) = await DidUtils.CreateAndStoreMyDidAsync(_store, _walletRecordServiceV2, seed: TestConstants.SenderSeed);
            _ = await Did.CreateAndStoreMyDidAsync(_wallet.Wallet, new { seed = TestConstants.SenderSeed }.ToJson());
            var (_, myRecipientVerkey) = await DidUtils.CreateAndStoreMyDidAsync(_store, _walletRecordServiceV2, seed: TestConstants.RecipientSeed);
            _ = await Did.CreateAndStoreMyDidAsync(_wallet.Wallet, new { seed = TestConstants.RecipientSeed }.ToJson());

            var packed = await CryptoUtils.PackAsync(_wallet, myRecipientVerkey, message, mySenderVerkey , recordService: null);
            var unpack = await CryptoUtils.UnpackAsync(_store, packed, recordService: _walletRecordServiceV2);

            var jObject = JObject.Parse(unpack.Message);

            Assert.NotNull(unpack);
            Assert.NotNull(unpack.SenderVerkey);
            Assert.NotNull(unpack.RecipientVerkey);
            Assert.Equal(unpack.RecipientVerkey, myRecipientVerkey);
            Assert.Equal(unpack.SenderVerkey, mySenderVerkey);
            Assert.Equal(MessageTypes.ConnectionInvitation, jObject["@type"].ToObject<string>());
        }
    }
}
