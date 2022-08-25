using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Indy.WalletApi;

namespace Hyperledger.Aries.Storage
{
    /// <inheritdoc />
    public class DefaultWalletService : IWalletService
    {
        /// <summary>
        /// Dictionary of open wallets
        /// </summary>
        protected static readonly ConcurrentDictionary<string, AriesStorage> Storages =
            new ConcurrentDictionary<string, AriesStorage>();

        /// <summary>
        /// Mutex semaphore for opening a new (not cached) wallet
        /// </summary>
        private static readonly SemaphoreSlim OpenWalletSemaphore = new SemaphoreSlim(1,1);

        /// <inheritdoc />
        public virtual async Task<AriesStorage> GetWalletAsync(WalletConfiguration configuration, WalletCredentials credentials)
        {
            AriesStorage ariesStorage = GetWalletFromCache(configuration);

            if (ariesStorage.Wallet == null)
            {
                ariesStorage = await OpenWalletWithMutexAsync(configuration, credentials);
            }

            return ariesStorage;
        }

        private async Task<AriesStorage> OpenWalletWithMutexAsync(WalletConfiguration configuration, WalletCredentials credentials)
        {
            AriesStorage ariesStorage;

            await OpenWalletSemaphore.WaitAsync();
            try
            {
                ariesStorage = GetWalletFromCache(configuration);

                if (ariesStorage.Wallet == null)
                {
                    ariesStorage.Wallet = await Wallet.OpenWalletAsync(configuration.ToJson(), credentials.ToJson());
                    Storages.TryAdd(configuration.Id, ariesStorage);
                }
            }
            finally
            {
                OpenWalletSemaphore.Release();
            }

            return ariesStorage;
        }

        private AriesStorage GetWalletFromCache(WalletConfiguration configuration)
        {
            if (Storages.TryGetValue(configuration.Id, out var ariesStorage))
            {
                if (ariesStorage.Wallet.IsOpen)
                    return ariesStorage;

                Storages.TryRemove(configuration.Id, out ariesStorage);
            }
            return new AriesStorage();
        }

        /// <inheritdoc />
        public virtual async Task<AriesStorage> CreateWalletAsync(WalletConfiguration configuration, WalletCredentials credentials)
        {
            await Wallet.CreateWalletAsync(configuration.ToJson(), credentials.ToJson());
            return new AriesStorage();
        }

        /// <inheritdoc />
        public virtual async Task DeleteWalletAsync(WalletConfiguration configuration, WalletCredentials credentials)
        {
            if (Storages.TryRemove(configuration.Id, out var ariesStorage))
            {
                if (ariesStorage.Wallet.IsOpen)
                    await ariesStorage.Wallet.CloseAsync();

                ariesStorage.Wallet.Dispose();
            }
            await Wallet.DeleteWalletAsync(configuration.ToJson(), credentials.ToJson());
        }
    }
}
