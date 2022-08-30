using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using aries_askar_dotnet.Models;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Storage.Models;
using AriesAskarStore = aries_askar_dotnet.AriesAskar.StoreApi;

namespace Hyperledger.Aries.Storage
{
    /// <inheritdoc />
    public class DefaultWalletServiceV2 : IWalletService
    {
        /// <summary>
        /// Dictionary of open wallets
        /// </summary>
        protected static readonly ConcurrentDictionary<string, AriesStorage> Storages =
            new ConcurrentDictionary<string, AriesStorage>();

        /// <summary>
        /// Mutex semaphore for opening a new (not cached) wallet
        /// </summary>
        private static readonly SemaphoreSlim OpenWalletSemaphore = new SemaphoreSlim(1, 1);

        /// <inheritdoc />
        public virtual async Task<AriesStorage> GetWalletAsync(WalletConfiguration configuration, WalletCredentials credentials)
        {

            AriesStorage ariesStorage = GetWalletFromCache(configuration);

            if (ariesStorage.Store == null)
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

                if (ariesStorage.Store == null)
                {
                    /** TODO : ??? - Other Input parameter needed like KeyDerivationMethod, profile? **/
                    ariesStorage.Store = await AriesAskarStore.OpenAsync(await BuildSpecUriAsync(configuration), passKey: credentials.Key);
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
                if (ariesStorage.Store is null)
                {
                    throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Store)} which must not be null.");
                }
                if (ariesStorage.Store.storeHandle != (IntPtr)0)
                    return ariesStorage;

                Storages.TryRemove(configuration.Id, out ariesStorage);
            }
            return new AriesStorage();
        }

        /// <inheritdoc />
        public virtual async Task CreateWalletAsync(WalletConfiguration configuration, WalletCredentials credentials)
        {
            /** TODO : ??? - Other Input parameter needed like KeyDerivationMethod, profile? **/
            Store store = await AriesAskarStore.ProvisionAsync(await BuildSpecUriAsync(configuration), passKey: credentials.Key);
            /** Need to close it again, cause here we just create the store backend. Analog to the <see cref="DefaultWalletService" />.**/
            await AriesAskarStore.CloseAsync(store);
        }

        /// <inheritdoc />
        public virtual async Task DeleteWalletAsync(WalletConfiguration configuration, WalletCredentials credentials)
        {
            //TODO : ??? - Check if there are remaining stores with same specUris left in Storages? Deletion of database is only possible if no active prozess onto the store is left. 
            if (Storages.TryRemove(configuration.Id, out var ariesStorage))
            {
                if (ariesStorage.Store is null)
                {
                    throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Store)} which must not be null.");
                }
                await AriesAskarStore.CloseAsync(ariesStorage.Store, remove: true);
            }
            else
            {
                string specUri = await BuildSpecUriAsync(configuration);
                await AriesAskarStore.RemoveAsync(new Store(new IntPtr(), specUri), specUri);
            }
        }

        private Task<string> BuildSpecUriAsync(WalletConfiguration configuration)
        {
            if (configuration != null)
            {
                if (configuration.StorageConfiguration != null)
                {
                    return Task.FromResult(configuration.StorageType + "://" + configuration.StorageConfiguration.Path);
                }
                else throw new ArgumentNullException(nameof(configuration.StorageConfiguration));
            }
            else throw new ArgumentNullException(nameof(configuration));
        }
    }
}
