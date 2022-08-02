﻿using System.Threading.Tasks;
using aries_askar_dotnet.Models;

namespace Hyperledger.Aries.Storage
{
    /// <summary>
    /// Wallet service.
    /// </summary>
    public interface INewWalletService
    {
        /// <summary>
        /// Gets the wallet async.
        /// </summary>
        /// <returns>The wallet async.</returns>
        /// <param name="configuration">Configuration.</param>
        /// <param name="credentials">Credentials.</param>
        Task<Store> GetWalletAsync(WalletConfiguration configuration, WalletCredentials credentials);

        /// <summary>
        /// Creates the wallet async.
        /// </summary>
        /// <returns>The wallet async.</returns>
        /// <param name="configuration">Configuration.</param>
        /// <param name="credentials">Credentials.</param>
        Task CreateWalletAsync(WalletConfiguration configuration, WalletCredentials credentials);

        /// <summary>
        /// Deletes the wallet async.
        /// </summary>
        /// <returns>Async Task</returns>
        /// <param name="configuration">Configuration.</param>
        /// <param name="credentials">Credentials.</param>
        Task DeleteWalletAsync(WalletConfiguration configuration, WalletCredentials credentials);
    }
}
