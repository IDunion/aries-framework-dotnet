using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Ledger.Abstractions;
using Hyperledger.Aries.Storage.Models;
using System;
using System.Threading.Tasks;
using IndyLedger = Hyperledger.Indy.LedgerApi.Ledger;

namespace Hyperledger.Aries.Ledger
{
    /// <inheritdoc />
    public class DefaultLedgerSigningService : ILedgerSigningService
    {
        private readonly IProvisioningService provisioningService;

        public DefaultLedgerSigningService(IProvisioningService provisioningService)
        {
            this.provisioningService = provisioningService;
        }
        /// <inheritdoc />
        public async Task<string> SignRequestAsync(IAgentContext context, string submitterDid, string requestJson)
        {
            try
            {
                ProvisioningRecord provisioning = await provisioningService.GetProvisioningAsync(context.AriesStorage);

                if (provisioning?.TaaAcceptance != null)
                {
                    requestJson = await IndyLedger.AppendTxnAuthorAgreementAcceptanceToRequestAsync(requestJson, provisioning.TaaAcceptance.Text,
                        provisioning.TaaAcceptance.Version, provisioning.TaaAcceptance.Digest, provisioning.TaaAcceptance.AcceptanceMechanism, (ulong)DateTimeOffset.Now.ToUnixTimeSeconds());
                }
            }
            catch (AriesFrameworkException ex) when (ex.ErrorCode == ErrorCode.RecordNotFound)
            {
                // OK, used in unit tests and scenarios when we want to simply send ledger commands
            }
            return await SignRequestAsync(context.AriesStorage, submitterDid, requestJson);
        }

        /// <inheritdoc />
        public Task<string> SignRequestAsync(AriesStorage storage, string submitterDid, string requestJson)
        {
            return IndyLedger.SignRequestAsync(storage.Wallet, submitterDid, requestJson);
        }
    }
}
