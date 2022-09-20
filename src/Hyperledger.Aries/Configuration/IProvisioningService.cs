using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Ledger.Models;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Indy.WalletApi;

namespace Hyperledger.Aries.Configuration
{
    /// <summary>
    /// Provisioning Service.
    /// </summary>
    public interface IProvisioningService
    {
        /// <summary>
        /// Returns the agent provisioning record. This is a single record that contains all
        /// agent configuration parameters.
        /// </summary>
        /// <param name="storage">The indy-sdk or aries-askar Wallet</param>
        /// <exception cref="AriesFrameworkException">Throws with ErrorCode.RecordNotFound.</exception>
        /// <returns>The provisioning record.</returns>
        Task<ProvisioningRecord> GetProvisioningAsync(AriesStorage storage);

        /// <summary>
        /// Creates a wallet and provisions a new agent with the default <see cref="AgentOptions" />
        /// </summary>
        /// <returns></returns>
        Task ProvisionAgentAsync();

        /// <summary>
        /// Creates a wallet and provisions a new agent with the specified <see cref="AgentOptions" />
        /// </summary>
        /// <returns></returns>
        Task ProvisionAgentAsync(AgentOptions agentOptions);

        /// <summary>
        /// Updates the agent endpoint information.
        /// </summary>
        /// <param name="storage">The indy-sdk or aries-askar Wallet.</param>
        /// <param name="endpoint">The endpoint.</param>
        /// <returns></returns>
        Task UpdateEndpointAsync(AriesStorage storage, AgentEndpoint endpoint);

        /// <summary>
        /// Accepts the transaction author agreement
        /// </summary>
        /// <param name="agentContext"></param>
        /// <param name="txnAuthorAgreement"></param>
        /// <param name="acceptanceMechanism"></param>
        /// <returns></returns>
        Task AcceptTxnAuthorAgreementAsync(IAgentContext agentContext, IndyTaa txnAuthorAgreement, string acceptanceMechanism = null);
    }
}
