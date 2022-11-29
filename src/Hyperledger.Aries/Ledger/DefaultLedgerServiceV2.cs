using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Ledger.Abstractions;
using Hyperledger.Aries.Ledger.Models;
using Hyperledger.Aries.Payments.Models;
using Hyperledger.Aries.Utils;
using indy_vdr_dotnet;
using indy_vdr_dotnet.libindy_vdr;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Ledger
{
    /// <inheritdoc />
    public class DefaultLedgerServiceV2 : ILedgerService
    {
        private readonly ILedgerSigningService _signingService;
        private readonly IPoolService _poolService;
        private readonly IProvisioningService _provisioningService;

        /// <summary>
        /// DefaultLedgerService using Indy-VDR to connect with indy ledgers
        /// </summary>
        /// <param name="signingService">Instance of <see cref="ILedgerSigningService"/>.</param>
        /// <param name="poolService">Instance of <see cref="IPoolService"/>.</param>
        /// <param name="provisioningService">Instance of <see cref="IProvisioningService"/>.</param>
        public DefaultLedgerServiceV2(ILedgerSigningService signingService, IPoolService poolService, IProvisioningService provisioningService)
        {
            _signingService = signingService;
            _poolService = poolService;
            _provisioningService = provisioningService;
        }

        /// <inheritdoc />
        public Task<IList<AuthorizationRule>> LookupAuthorizationRulesAsync(IAgentContext agentContext)
        {
            throw new NotImplementedException($"{nameof(LookupAuthorizationRulesAsync)} is not implemented");
        }

        /// <inheritdoc />
        public async Task<string> LookupAttributeAsync(IAgentContext agentContext, string targetDid, string attributeName)
        {
            IntPtr req = await LedgerApi.BuildGetAttributeRequest(targetDid, null, attributeName, null, null);

            string res = await SubmitRequestAsync(agentContext, req);

            JObject jobj = JObject.Parse(res);
            string data = jobj["result"]?["data"]?.ToString() ?? throw new ArgumentNullException(attributeName);

            string result = JObject.Parse(data)[attributeName]?.ToString();

            return result;
        }

        /// <inheritdoc />
        public async Task RegisterAttributeAsync(IAgentContext context, string submittedDid, string targetDid, string attributeName,
            object value, TransactionCost paymentInfo = null)
        {
            string data = $"{{\"{attributeName}\": {value.ToJson()}}}";

            IntPtr req = await LedgerApi.BuildAttributeRequest(targetDid, submittedDid, null, data);

            _ = await SignAndSubmitRequestAsync(context, submittedDid, req);
        }

        /// <inheritdoc />
        public async Task<AriesResponse> LookupSchemaAsync(IAgentContext agentContext, string schemaId)
        {
            IntPtr req = await LedgerApi.BuildGetSchemaRequestAsync(schemaId);

            string response = await SubmitRequestAsync(agentContext, req);

            return ResponseParser.ParseGetSchemaResponse(response);
        }

        /// <inheritdoc />
        public async Task<string> LookupNymAsync(IAgentContext agentContext, string did)
        {
            IntPtr req = await LedgerApi.BuildGetNymRequest(did);

            return await SubmitRequestAsync(agentContext, req);
        }

        /// <inheritdoc />
        public async Task<string> LookupTransactionAsync(IAgentContext agentContext, string ledgerType, int sequenceId)
        {
            if (int.TryParse(ledgerType, out int ledgerTypeParsed))
            {
                // Success
            }
            else
            {
                ledgerTypeParsed = 1; //Default Domain
            }

            IntPtr req = await LedgerApi.BuildGetTxnRequestAsync(ledgerTypeParsed, sequenceId);

            return await SubmitRequestAsync(agentContext, req);
        }

        /// <inheritdoc />
        public async Task<AriesResponse> LookupDefinitionAsync(IAgentContext agentContext, string definitionId)
        {
            IntPtr req = await LedgerApi.BuildGetCredDefRequest(definitionId);
            string res = await SubmitRequestAsync(agentContext, req);

            return ResponseParser.ParseGetCredDefResponse(definitionId, res);
        }

        /// <inheritdoc />
        public async Task<AriesResponse> LookupRevocationRegistryDefinitionAsync(IAgentContext agentContext, string registryId)
        {
            IntPtr req = await LedgerApi.BuildGetRevocRegDefRequest(registryId);
            string res = await SubmitRequestAsync(agentContext, req);

            return ResponseParser.ParseRegistryDefinitionResponse(registryId, res);
        }

        /// <inheritdoc />
        public async Task<AriesRegistryResponse> LookupRevocationRegistryDeltaAsync(IAgentContext agentContext, string revocationRegistryId, long from, long to)
        {
            IntPtr req = await LedgerApi.BuildGetRevocRegDeltaRequestAsync(revocationRegistryId, to, from);
            string res = await SubmitRequestAsync(agentContext, req);

            return ResponseParser.ParseRevocRegDeltaResponse(res);
        }

        /// <inheritdoc />
        public async Task<AriesRegistryResponse> LookupRevocationRegistryAsync(IAgentContext agentContext, string revocationRegistryId, long timestamp)
        {
            IntPtr req = await LedgerApi.BuildGetRevocRegRequest(revocationRegistryId, timestamp);
            string res = await SubmitRequestAsync(agentContext, req);

            return ResponseParser.ParseRevocRegResponse(res);
        }

        /// <inheritdoc />
        public async Task RegisterNymAsync(IAgentContext context, string submitterDid, string theirDid, string theirVerkey, string role,
            TransactionCost paymentInfo = null)
        {
            IntPtr req = await LedgerApi.BuildNymRequestAsync(submitterDid, theirDid, theirVerkey, role: role);

            _ = await SignAndSubmitRequestAsync(context, submitterDid, req);
        }

        /// <inheritdoc />
        public async Task RegisterCredentialDefinitionAsync(IAgentContext context, string submitterDid, string data,
            TransactionCost paymentInfo = null)
        {
            IntPtr req = await LedgerApi.BuildCredDefRequest(submitterDid, data);

            var res = await SignAndSubmitRequestAsync(context, submitterDid, req);
        }

        public async Task RegisterRevocationRegistryDefinitionAsync(IAgentContext context, string submitterDid, string data,
            TransactionCost paymentInfo = null)
        {
            IntPtr req = await LedgerApi.BuildRevocRegDefRequestAsync(submitterDid, data);

            string res = await SignAndSubmitRequestAsync(context, submitterDid, req);
        }

        /// <inheritdoc />
        public async Task SendRevocationRegistryEntryAsync(IAgentContext context, string issuerDid, string revocationRegistryDefinitionId,
            string revocationDefinitionType, string value, TransactionCost paymentInfo = null)
        {
            IntPtr req = await LedgerApi.BuildRevocRegEntryRequestAsync(issuerDid, revocationRegistryDefinitionId,
                revocationDefinitionType, value);

            string res = await SignAndSubmitRequestAsync(context, issuerDid, req);
        }

        /// <inheritdoc />
        public async Task RegisterSchemaAsync(IAgentContext context, string issuerDid, string schemaJson,
            TransactionCost paymentInfo = null)
        {
            IntPtr req = await LedgerApi.BuildSchemaRequestAsync(issuerDid, schemaJson);

            _ = await SignAndSubmitRequestAsync(context, issuerDid, req);
        }

        /// <inheritdoc />
        public async Task<ServiceEndpointResult> LookupServiceEndpointAsync(IAgentContext context, string did)
        {
            string response = await LookupAttributeAsync(context, did, "endpoint");

            string endpoint = JObject.Parse(response)["endpoint"]?.ToString();

            return new ServiceEndpointResult { Result = new ServiceEndpointResult.ServiceEndpoint { Endpoint = endpoint } };
        }

        /// <inheritdoc />
        public Task RegisterServiceEndpointAsync(IAgentContext context, string did, string serviceEndpoint,
            TransactionCost paymentInfo = null)
        {
            var value = new { endpoint = serviceEndpoint };
            return RegisterAttributeAsync(context, did, did, "endpoint", value);
        }


        /// <summary>
        /// Adds transaction author agreement, sign and submit the ledger request.
        /// </summary>
        /// <param name="context">The agent context.</param>
        /// <param name="signingDid">The signing did.</param>
        /// <param name="requestHandle">The request handle.</param>
        /// <returns>The result of the <see cref="SubmitRequestAsync"/> method of the given request.</returns>
        protected async Task<string> SignAndSubmitRequestAsync(IAgentContext context, string signingDid, IntPtr requestHandle)
        {
            ProvisioningRecord provisioning = await _provisioningService.GetProvisioningAsync(context.AriesStorage);
            if (provisioning?.TaaAcceptance != null)
            {
                string agreementAcceptance = await RequestApi.PrepareTxnAuthorAgreementAcceptanceAsync(
                    provisioning.TaaAcceptance.AcceptanceMechanism,
                    (ulong)DateTimeOffset.Now.ToUnixTimeSeconds(),
                    provisioning.TaaAcceptance.Text,
                    provisioning.TaaAcceptance.Version,
                    provisioning.TaaAcceptance.Digest);

                await RequestApi.RequestSetTxnAuthorAgreementAcceptanceAsync(requestHandle, agreementAcceptance);
            }

            string unsignedRequest = await RequestApi.RequestGetSignatureInputAsync(requestHandle);
            string signature = await _signingService.SignRequestAsync(context, signingDid, unsignedRequest);
            await RequestApi.RequestSetSigantureAsync(requestHandle, Convert.FromBase64String(signature));

            return await SubmitRequestAsync(context, requestHandle);
        }

        /// <summary>
        /// Submit the ledger request.
        /// </summary>
        /// <param name="context">The agent context.</param>
        /// <param name="requestHandle">The ledger request.</param>
        /// <returns>Result of the <see cref="IPoolService.SubmitRequestAsync"/> method for the given request.</returns>
        protected async Task<string> SubmitRequestAsync(IAgentContext context, IntPtr requestHandle)
        {
            async Task<string> SubmitAsync()
            {
                return await _poolService.SubmitRequestAsync(context.Pool, requestHandle);
            }

            return await ResilienceUtils.RetryPolicyAsync(
                action: SubmitAsync,
                exceptionPredicate: (IndyVdrException e) => e.Message.Contains("PoolTimeout") ||
                                                            e.Message.Contains("Service unavailable"));
        }
    }
}
