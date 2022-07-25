using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Contracts;
using IndyVdrRequest = indy_vdr_dotnet.libindy_vdr.RequestApi;
using IndyVdrLedger = indy_vdr_dotnet.libindy_vdr.LedgerApi;
using Hyperledger.Indy.WalletApi;
using Hyperledger.Aries.Configuration;
using System;
using aries_askar_dotnet.Models;

namespace Hyperledger.Aries.Ledger
{
    /// <inheritdoc />
    public class NewLedgerSigningService : INewLedgerSigningService
    {
        private readonly INewProvisioningService provisioningService;

        public NewLedgerSigningService(INewProvisioningService provisioningService)
        {
            this.provisioningService = provisioningService;
        }
        /// <inheritdoc />
        public async Task<string> SignRequestAsync(IAgentContext context, string submitterDid, string requestJson)
        {
            try
            {
                var provisioning = await provisioningService.GetProvisioningAsync(context.WalletStore);

                if (provisioning?.TaaAcceptance != null)
                {
                    IntPtr requestHandle = await IndyVdrLedger.BuildCustomRequest(requestJson); 

                    string taaRequestJson = await IndyVdrRequest.PrepareTxnAuthorAgreementAcceptanceAsync(
                        provisioning.TaaAcceptance.AcceptanceMechanism,
                        (ulong)DateTimeOffset.Now.ToUnixTimeSeconds(),
                        provisioning.TaaAcceptance.Text,
                        provisioning.TaaAcceptance.Version,
                        provisioning.TaaAcceptance.Digest);
                    await IndyVdrRequest.RequestSetTxnAuthorAgreementAcceptanceAsync(requestHandle, taaRequestJson);

                    requestJson = await IndyVdrRequest.RequestGetBodyAsync(requestHandle);
                }
            }
            catch (AriesFrameworkException ex) when (ex.ErrorCode == ErrorCode.RecordNotFound)
            {
                // OK, used in unit tests and scenarios when we want to simply send ledger commands
            }
            return await SignRequestAsync(context.WalletStore, submitterDid, requestJson);
        }

        /// <inheritdoc />
        public Task<string> SignRequestAsync(Store wallet, string submitterDid, string requestJson)
        {
            string signature = "???"; //TODO: ??? GetSignature from Wallet and submitterDid info?
            IntPtr requestHandle = IndyVdrLedger.BuildCustomRequest(requestJson).GetAwaiter().GetResult();
            IndyVdrRequest.RequestSetSigantureAsync(requestHandle, signature);
            return IndyVdrRequest.RequestGetBodyAsync(requestHandle);
        }
    }
}
