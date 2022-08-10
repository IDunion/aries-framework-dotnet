﻿using System.Threading.Tasks;
using aries_askar_dotnet.Models;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Ledger;

namespace Hyperledger.Aries.Configuration
{
    /// <summary>
    /// Provisioning Service.
    /// </summary>
    public interface INewProvisioningService
    {
        /// <summary>
        /// Returns the agent provisioning record. This is a single record that contains all
        /// agent configuration parameters.
        /// </summary>
        /// <param name="wallet">The wallet.</param>
        /// <exception cref="AriesFrameworkException">Throws with ErrorCode.RecordNotFound.</exception>
        /// <returns>The provisioning record.</returns>
        Task<ProvisioningRecord> GetProvisioningAsync(Store wallet);

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
        /// <param name="wallet">The wallet.</param>
        /// <param name="endpoint">The endpoint.</param>
        /// <returns></returns>
        Task UpdateEndpointAsync(Store wallet, AgentEndpoint endpoint);

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