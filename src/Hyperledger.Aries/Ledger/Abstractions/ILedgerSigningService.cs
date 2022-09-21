using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Storage.Models;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Ledger.Abstractions
{
    /// <summary>
    /// Ledger Signing Service
    /// </summary>
    public interface ILedgerSigningService
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
        /// <param name="storage"></param>
        /// <param name="submitterDid"></param>
        /// <param name="requestJson"></param>
        /// <returns></returns>
        Task<string> SignRequestAsync(AriesStorage storage, string submitterDid, string requestJson);
    }
}
