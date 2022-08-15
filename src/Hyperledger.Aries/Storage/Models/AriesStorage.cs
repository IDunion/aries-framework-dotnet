using aries_askar_dotnet.Models;
using Hyperledger.Indy.WalletApi;

namespace Hyperledger.Aries.Storage.Models
{
    public class AriesStorage
    {
        /// <summary>
        /// The indy-sdk version of wallet
        /// </summary>
        public Wallet Wallet { get; set; }

        /// <summary>
        /// The aries-askar version of wallet
        /// </summary>
        public Store Store { get; set; }
    }
}
