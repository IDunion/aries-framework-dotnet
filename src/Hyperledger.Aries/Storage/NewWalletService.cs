using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using aries_askar_dotnet.Models;
using Hyperledger.Aries.Extensions;
using AriesAskarStore = aries_askar_dotnet.AriesAskar.StoreApi;

namespace Hyperledger.Aries.Storage
{
    /// <inheritdoc />
    public class NewWalletService : INewWalletService
    {
        /// <summary>
        /// Dictionary of open wallets
        /// </summary>
        protected static readonly ConcurrentDictionary<string, Store> Wallets =
            new ConcurrentDictionary<string, Store>();

        /// <summary>
        /// Mutex semaphore for opening a new (not cached) wallet
        /// </summary>
        private static readonly SemaphoreSlim OpenWalletSemaphore = new SemaphoreSlim(1, 1);

        /// <inheritdoc />
        public virtual async Task<Store> GetWalletAsync(WalletConfiguration configuration, WalletCredentials credentials)
        {
            Store wallet = GetWalletFromCache(configuration);

            if (wallet == null)
            {
                wallet = await OpenWalletWithMutexAsync(configuration, credentials);
            }

            return wallet;
        }

        private async Task<Store> OpenWalletWithMutexAsync(WalletConfiguration configuration, WalletCredentials credentials)
        {
            Store wallet;

            await OpenWalletSemaphore.WaitAsync();
            try
            {
                wallet = GetWalletFromCache(configuration);

                if (wallet == null)
                {
                    /** TODO : ??? - check for right parameters, maybe we need to build the specUri string from the configuration/credential inputs **/
                    wallet = await AriesAskarStore.OpenAsync(specUri : configuration.StorageConfiguration.Url);
                    Wallets.TryAdd(configuration.Id, wallet);
                }
            }
            finally
            {
                OpenWalletSemaphore.Release();
            }

            return wallet;
        }

        private Store GetWalletFromCache(WalletConfiguration configuration)
        {
            if (Wallets.TryGetValue(configuration.Id, out var wallet))
            {
                if (wallet.storeHandle != (IntPtr)0)
                    return wallet;

                Wallets.TryRemove(configuration.Id, out wallet);
            }
            return null;
        }

        /// <inheritdoc />
        public virtual async Task CreateWalletAsync(WalletConfiguration configuration, WalletCredentials credentials)
        {
            /** TODO : ??? - check for right parameters, maybe we need to build the specUri string from the configuration/credential inputs **/
            await AriesAskarStore.ProvisionAsync(specUri: configuration.StorageConfiguration.Url);
        }

        /// <inheritdoc />
        public virtual async Task DeleteWalletAsync(WalletConfiguration configuration, WalletCredentials credentials)
        {
            if (Wallets.TryRemove(configuration.Id, out var wallet))
            {
                await AriesAskarStore.CloseAsync(wallet);
            }
            /** TODO : ??? - check for right parameters, maybe we need to build the specUri string from the configuration/credential inputs **/
            await AriesAskarStore.RemoveAsync(wallet, specUri: configuration.StorageConfiguration.Url);
        }
    }
}
