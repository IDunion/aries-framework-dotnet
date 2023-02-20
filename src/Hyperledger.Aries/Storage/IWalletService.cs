using Hyperledger.Aries.Storage.Models;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Storage
{
    /// <summary>
    /// Wallet service.
    /// </summary>
    public interface IWalletService
    {
        /// <summary>
        /// Gets the wallet async.
        /// </summary>
        /// <returns>The wallet async.</returns>
        /// <param name="configuration">Configuration.</param>
        /// <param name="credentials">Credentials.</param>
        Task<AriesStorage> GetWalletAsync(WalletConfiguration configuration, WalletCredentials credentials);

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

        /// <summary>
        /// Changes the wallet key async.
        /// </summary>
        /// <returns>Boolean flag which indicates success or not. True if successfull, otherwise false.</returns>
        /// <param name="newKey">The new key for decrypting/encrypting the wallet.</param>
        /// <param name="configuration">Configuration.</param>
        /// <param name="oldCredentials">Credentials of the wallet containing the old key.</param>
        Task<bool> ChangeWalletKeyAsync(string newKey, WalletConfiguration configuration, WalletCredentials oldCredentials);

        /// <summary>
        /// Closes the wallet async.
        /// </summary>
        /// <returns>Async Task</returns>
        /// <param name="configuration">Configuration.</param>
        Task CloseWalletAsync(WalletConfiguration configuration);

        /// <summary>
        /// Create a wallet key async.
        /// </summary>
        /// <returns>Async Task</returns>
        /// <param name="seed">wallet key seed.</param>
        /// <returns>The wallet key.</returns>
        Task<string> CreateWalletKeyAsync(string seed = null);
    }
}
