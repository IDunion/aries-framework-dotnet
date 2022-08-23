using System;
using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Decorators.Signature;
using Hyperledger.Aries.Features.Handshakes.Common;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Indy.CryptoApi;
using Hyperledger.Indy.WalletApi;
using Xunit;

namespace Hyperledger.Aries.Tests.Decorators
{
    public class SignatorDecoratorTests : IAsyncLifetime
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
            
            //_agent = new DefaultAgentContext
            //{
            //    Wallet = await Wallet.OpenWalletAsync(_walletConfig, Credentials),
            //};
            // TODO ??? correct refactor?
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
        public async Task CanSignData()
        {
            var data = new Connection
            {
                Did = "test"
            };

            var key = await Crypto.CreateKeyAsync(_agent.AriesStorage.Wallet, "{}");

            var sigData = await SignatureUtils.SignDataAsync(_agent, data, key);
            
            Assert.True(sigData.SignatureType == SignatureUtils.DefaultSignatureType);
            Assert.NotNull(sigData.Signature);
            Assert.NotNull(sigData.SignatureData);
            Assert.NotNull(sigData.Signer);
        }

        [Fact]
        public async Task CanSignAndVerifyData()
        {
            var data = new Connection
            {
                Did = "test"
            };

            var key = await Crypto.CreateKeyAsync(_agent.AriesStorage.Wallet, "{}");

            var sigData = await SignatureUtils.SignDataAsync(_agent, data, key);
            
            var result = await SignatureUtils.UnpackAndVerifyAsync<Connection>(sigData, _agent);

            Assert.True(data.Did == result.Did);
        }
    }
}
