﻿using anoncreds_rs_dotnet;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Indy.DidApi;
using Hyperledger.Indy.WalletApi;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Storage
{
    /// <inheritdoc />
    public class DefaultWalletService : IWalletService
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
                if (ariesStorage.Wallet.IsOpen)
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
            await Wallet.CreateWalletAsync(configuration.ToJson(), credentials.ToJson());
        }

        /// <inheritdoc />
        public virtual async Task DeleteWalletAsync(WalletConfiguration configuration, WalletCredentials credentials)
        {
            if (Storages.TryRemove(configuration.Id, out AriesStorage ariesStorage))
            {
                if (ariesStorage.Wallet.IsOpen)
                {
                    await ariesStorage.Wallet.CloseAsync();
                }

                ariesStorage.Wallet.Dispose();
            }
            await Wallet.DeleteWalletAsync(configuration.ToJson(), credentials.ToJson());
        }

        /// <inheritdoc />
        public async Task<string> CreateWalletKeyAsync(string seed = null)
        {
            return await Wallet.GenerateWalletKeyAsync(new { seed }.ToJson());

        }

        /// <inheritdoc />
        public Task<bool> ChangeWalletKeyAsync(string newKey, WalletConfiguration configuration, WalletCredentials oldCredentials)
        {
            throw new System.NotImplementedException();
        }
        /// <inheritdoc />
        public Task CloseWalletAsync(WalletConfiguration configuration)
        {
            throw new System.NotImplementedException();
        }
    }
}
