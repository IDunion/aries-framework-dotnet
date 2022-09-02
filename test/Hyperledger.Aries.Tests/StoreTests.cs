using System;
using System.Threading.Tasks;
using aries_askar_dotnet;
using aries_askar_dotnet.AriesAskar;
using aries_askar_dotnet.Models;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Indy.WalletApi;
using Hyperledger.TestHarness;
using Xunit;
using AriesAskarStore = aries_askar_dotnet.AriesAskar.StoreApi;

namespace Hyperledger.Aries.Tests
{
    public class StoreTests : IAsyncLifetime
    {
        protected IWalletService _walletService;
        protected WalletConfiguration _config;
        protected WalletCredentials _creds;
        public Task InitializeAsync()
        {
            _walletService = new DefaultWalletServiceV2();

            _config = TestConstants.TestSingleWalletV2WalletConfig;
            _creds = TestConstants.TestSingelWalletV2WalletCreds;
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            await _walletService.DeleteWalletAsync(_config, _creds);
        }

        [Fact]
        public async Task ConcurrentWalletAccess()
        {
            await _walletService.CreateWalletAsync(_config, _creds);

            Task<AriesStorage> openWalletTask1 = _walletService.GetWalletAsync(_config, _creds);
            Task<AriesStorage> openWalletTask2 = _walletService.GetWalletAsync(_config, _creds);
            Task<AriesStorage> openWalletTask3 = _walletService.GetWalletAsync(_config, _creds);
            Task<AriesStorage> openWalletTask4 = _walletService.GetWalletAsync(_config, _creds);

            await Task.WhenAll(openWalletTask1, openWalletTask2, openWalletTask3, openWalletTask4);

            Assert.NotEqual(default, openWalletTask1.Result.Store.storeHandle);
            Assert.NotEqual(default, openWalletTask2.Result.Store.storeHandle);
            Assert.NotEqual(default, openWalletTask3.Result.Store.storeHandle);
            Assert.NotEqual(default, openWalletTask4.Result.Store.storeHandle);
        }

        [Fact]
        public async Task CanCreateAndGetWallet()
        {
            await _walletService.CreateWalletAsync(_config, _creds);

            var storage = await _walletService.GetWalletAsync(_config, _creds);

            Assert.NotNull(storage.Store);
            Assert.NotEqual(default, storage.Store.storeHandle);
        }

        [Fact(DisplayName = "Should create wallet with RAW key derivation")]
        public async Task CanCreateWallet_WhenRawKeyDerivationIsUsed()
        {
            await _walletService.CreateWalletAsync(_config, TestConstants.TestSingelWalletCredsRawEncoding);

            var storage = await _walletService.GetWalletAsync(_config, TestConstants.TestSingelWalletCredsRawEncoding);

            Assert.NotNull(storage.Store);
            Assert.NotEqual(default, storage.Store.storeHandle);
        }

        [Fact]
        public async Task CanCreateGetAndCloseWallet()
        {
            await _walletService.CreateWalletAsync(_config, _creds);

            var storage = await _walletService.GetWalletAsync(_config, _creds);

            Assert.NotNull(storage.Store);
            Assert.NotEqual(default, storage.Store.storeHandle);

            await AriesAskarStore.CloseAsync(storage.Store);

            storage = await _walletService.GetWalletAsync(_config, _creds);

            Assert.NotNull(storage.Store);
            Assert.NotEqual(default, storage.Store.storeHandle);
        }

        [Fact]
        public async Task CanCreateGetAndDeleteWallet()
        {
            await _walletService.CreateWalletAsync(_config, _creds);

            var storage = await _walletService.GetWalletAsync(_config, _creds);

            Assert.NotNull(storage.Store);
            Assert.NotEqual(default, storage.Store.storeHandle);

            await _walletService.DeleteWalletAsync(_config, _creds);

            await Assert.ThrowsAsync<AriesAskarException>(() => _walletService.GetWalletAsync(_config, _creds));
        }
    }
}
