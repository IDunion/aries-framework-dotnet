using System.Threading.Tasks;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Indy.WalletApi;

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
        /// /*** TODO : ??? - change to Task with no return when Provisiong/OpenAsync is fixed***/
        Task<AriesStorage> CreateWalletAsync(WalletConfiguration configuration, WalletCredentials credentials);

        /// <summary>
        /// Deletes the wallet async.
        /// </summary>
        /// <returns>Async Task</returns>
        /// <param name="configuration">Configuration.</param>
        /// <param name="credentials">Credentials.</param>
        Task DeleteWalletAsync(WalletConfiguration configuration, WalletCredentials credentials);
    }
}
