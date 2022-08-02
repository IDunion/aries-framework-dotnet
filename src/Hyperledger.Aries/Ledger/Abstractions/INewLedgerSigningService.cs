using System.Threading.Tasks;
using aries_askar_dotnet.Models;
using Hyperledger.Aries.Agents;
using Hyperledger.Indy.WalletApi;

namespace Hyperledger.Aries.Contracts
{
    /// <summary>
    /// Ledger Signing Service
    /// </summary>
    public interface INewLedgerSigningService
    {
        /// <summary>
        /// Signs the outgoing request
        /// </summary>
        /// <param name="context"></param>
        /// <param name="submitterDid"></param>
        /// <param name="requestJson"></param>
        /// <returns></returns>
        Task<string> SignRequestAsync(IAgentContext context, string submitterDid, string requestJson);

        /// <summary>
        /// Signs the outgoing request
        /// </summary>
        /// <param name="wallet"></param>
        /// <param name="submitterDid"></param>
        /// <param name="requestJson"></param>
        /// <returns></returns>
        Task<string> SignRequestAsync(Store wallet, string submitterDid, string requestJson);
    }
}
