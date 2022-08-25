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
                    /** TODO : ??? - check for right parameters, maybe we need to build the specUri string from the configuration/credential inputs **/
                    ariesStorage.Store = await AriesAskarStore.OpenAsync(specUri : configuration.StorageConfiguration.Url);
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
        public virtual async Task<AriesStorage> CreateWalletAsync(WalletConfiguration configuration, WalletCredentials credentials)
        {
            /** TODO : ??? - check for right parameters, maybe we need to build the specUri string from the configuration/credential inputs **/
            //return new AriesStorage(store: await AriesAskarStore.ProvisionAsync(specUri: configuration.StorageConfiguration.Url));
            var ariesStorage = new AriesStorage(store: await AriesAskarStore.ProvisionAsync("sqlite://:memory:"));//specUri: configuration.StorageConfiguration.Url));
            Storages.TryAdd(configuration.Id, ariesStorage);
            return ariesStorage;
        }

        /// <inheritdoc />
        public virtual async Task DeleteWalletAsync(WalletConfiguration configuration, WalletCredentials credentials)
        {
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
                /** TODO : ??? - check for right parameters, maybe we need to build the specUri string from the configuration/credential inputs **/
                string specUri = configuration.StorageConfiguration.Url;
                await AriesAskarStore.RemoveAsync(new Store(new IntPtr(), specUri), specUri: specUri);
            }
        }
    }
}
