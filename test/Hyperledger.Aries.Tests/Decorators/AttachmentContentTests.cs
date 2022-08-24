using System;
using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Decorators.Attachments;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Indy.CryptoApi;
using Hyperledger.Indy.WalletApi;
using Xunit;

namespace Hyperledger.Aries.Tests.Decorators
{
    public class AttachmentContentTests : IAsyncLifetime
    {
        private readonly string _walletConfig = $"{{\"id\":\"{Guid.NewGuid()}\"}}";
        private const string Credentials = "{\"key\":\"test_wallet_key\"}";
        private IAgentContext _agent;

        public async Task InitializeAsync()
        {
            try
            {
                await Wallet.CreateWalletAsync(_walletConfig, Credentials);
            }
            catch (WalletExistsException)
            {
                // OK
            }
            
            _agent = new DefaultAgentContext
            {
                AriesStorage = new AriesStorage(wallet: await Wallet.OpenWalletAsync(_walletConfig, Credentials)),
            };
        }

        public async Task DisposeAsync()
        {
            if (_agent != null) await _agent.AriesStorage.Wallet.CloseAsync();
            await Wallet.DeleteWalletAsync(_walletConfig, Credentials);
        }

        [Fact]
        public async Task CanSignAttachmentContent()
        {
            var base64Content = "Hello World!".ToBase64Url();
            var content = new AttachmentContent {Base64 = base64Content};
            var key = await Crypto.CreateKeyAsync(_agent.AriesStorage.Wallet, "{}");

            await content.SignWithJsonWebSignature(_agent.AriesStorage, key);
            
            Assert.NotNull(content.JsonWebSignature);
            Assert.NotNull(content.JsonWebSignature.Header);
            Assert.NotNull(content.JsonWebSignature.Protected);
            Assert.NotNull(content.JsonWebSignature.Signature);
        }

        [Fact]
        public async Task SignAttachmentThrowsIfContentIsNull()
        {
            var content = new AttachmentContent();
            var key = await Crypto.CreateKeyAsync(_agent.AriesStorage.Wallet, "{}");

            await Assert.ThrowsAsync<NullReferenceException>(async () => await content.SignWithJsonWebSignature(_agent.AriesStorage, key));
        }

        [Fact]
        public async Task CanVerifySignedAttachmentContent()
        {
            var base64Content = "Hello World!".ToBase64Url();
            var content = new AttachmentContent {Base64 = base64Content};
            var key = await Crypto.CreateKeyAsync(_agent.AriesStorage.Wallet, "{}");
            await content.SignWithJsonWebSignature(_agent.AriesStorage, key);

            var result = await content.VerifyJsonWebSignature(_agent);
            
            Assert.True(result);
        }

        [Fact]
        public async Task VerifyReturnsFalseWithWrongSignature()
        {
            var base64Content = "Hello World!".ToBase64Url();
            var content = new AttachmentContent {Base64 = base64Content};
            var key = await Crypto.CreateKeyAsync(_agent.AriesStorage.Wallet, "{}");
            await content.SignWithJsonWebSignature(_agent.AriesStorage, key);

            content.Base64 = "Changed content".ToBase64Url();

            var result = await content.VerifyJsonWebSignature(_agent);
            
            Assert.False(result);
        }
        
        [Fact]
        public async Task VerifyReturnsFalseIfSignatureIsNull()
        {
            var base64Content = "Hello World!".ToBase64Url();
            var content = new AttachmentContent {Base64 = base64Content};

            var result = await content.VerifyJsonWebSignature(_agent);
            
            Assert.False(result);
        }
    }
}
