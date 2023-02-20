using aries_askar_dotnet.Models;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Storage.Models;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
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
            new();

        /// <summary>
        /// Mutex semaphore for opening a new (not cached) wallet
        /// </summary>
        private static readonly SemaphoreSlim OpenWalletSemaphore = new(1, 1);

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
                    //Other Input parameter needed like profile?
                    string keyDerivationMethod =
                        string.IsNullOrEmpty(credentials.KeyDerivationMethod) ? "none" : credentials.KeyDerivationMethod;
                    ariesStorage.Store = await AriesAskarStore.OpenAsync(
                        await BuildSpecUriAsync(configuration),
                        keyMethod: KeyMethodConverter.ToKeyMethod(keyDerivationMethod),
                        passKey: credentials.Key);

                    if (ariesStorage.Store.storeHandle == (IntPtr)0)
                        return new AriesStorage();

                    _ = Storages.TryAdd(configuration.Id, ariesStorage);
                }
            }
            finally
            {
                _ = OpenWalletSemaphore.Release();
            }

            return ariesStorage;
        }

        private AriesStorage GetWalletFromCache(WalletConfiguration configuration)
        {
            if (Storages.TryGetValue(configuration.Id, out AriesStorage ariesStorage))
            {
                if (ariesStorage.Store is null)
                {
                    throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Store)} which must not be null.");
                }
                if (ariesStorage.Store.storeHandle != (IntPtr)0)
                {
                    return ariesStorage;
                }

                _ = Storages.TryRemove(configuration.Id, out _);
            }
            return new AriesStorage();
        }

        /// <inheritdoc />
        public virtual async Task CreateWalletAsync(WalletConfiguration configuration, WalletCredentials credentials)
        {
            //Other Input parameter needed like profile?
            string keyDerivationMethod =
                string.IsNullOrEmpty(credentials.KeyDerivationMethod) ? "none" : credentials.KeyDerivationMethod;

            Store store = await AriesAskarStore.ProvisionAsync(
                await BuildSpecUriAsync(configuration),
                keyMethod: KeyMethodConverter.ToKeyMethod(keyDerivationMethod),
                passKey: credentials.Key);
            // Need to close it again, cause here we just create the store backend. Analog to the <see cref="DefaultWalletService" />.
            _ = await AriesAskarStore.CloseAsync(store);
        }

        /// <inheritdoc />
        public virtual async Task DeleteWalletAsync(WalletConfiguration configuration, WalletCredentials credentials)
        {
            if (Storages.TryRemove(configuration.Id, out AriesStorage ariesStorage))
            {
                if (ariesStorage.Store is null)
                {
                    throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Store)} which must not be null.");
                }
                _ = await AriesAskarStore.CloseAsync(ariesStorage.Store, remove: true);
            }
            else
            {
                string specUri = await BuildSpecUriAsync(configuration);
                _ = await AriesAskarStore.RemoveAsync(new Store(new IntPtr(), specUri), specUri);
            }
        }

        /// <inheritdoc />
        public virtual async Task CloseWalletAsync(WalletConfiguration configuration)
        {
            if (Storages.TryRemove(configuration.Id, out AriesStorage ariesStorage))
            {
                if (ariesStorage.Store is null)
                {
                    throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Store)} which must not be null.");
                }
                _ = await AriesAskarStore.CloseAsync(ariesStorage.Store);
            }
        }

        /// <inheritdoc />
        public virtual async Task<bool> ChangeWalletKeyAsync(string newKey, WalletConfiguration configuration, WalletCredentials oldCredentials)
        {
            AriesStorage ariesStorage = await GetWalletAsync(configuration, oldCredentials);
            bool result = false;

            if (ariesStorage.Store != null && ariesStorage.Store.storeHandle != (IntPtr)0) 
            {
                result = await AriesAskarStore.RekeyAsync(
                   ariesStorage.Store,
                   KeyMethodConverter.ToKeyMethod(oldCredentials.KeyDerivationMethod),
                   newKey);

                await CloseWalletAsync(configuration);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<string> CreateWalletKeyAsync(string seed = null)
        {
            return await AriesAskarStore.GenerateRawKeyAsync(seed);
        }

        private Task<string> BuildSpecUriAsync(WalletConfiguration configuration)
        {
            if (configuration != null)
            {
                return configuration.StorageConfiguration != null
                    ? Task.FromResult(configuration.StorageType + "://" + configuration.StorageConfiguration.Path)
                    : throw new ArgumentNullException(nameof(configuration.StorageConfiguration));
            }
            else
            {
                throw new ArgumentNullException(nameof(configuration));
            }
        }
    }
}
