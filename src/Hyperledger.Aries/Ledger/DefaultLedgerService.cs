using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Ledger.Abstractions;
using Hyperledger.Aries.Ledger.Models;
using Hyperledger.Aries.Payments;
using Hyperledger.Aries.Payments.Models;
using Hyperledger.Aries.Utils;
using Hyperledger.Indy;
using Hyperledger.Indy.DidApi;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IndyLedger = Hyperledger.Indy.LedgerApi.Ledger;
using IndyPayments = Hyperledger.Indy.PaymentsApi.Payments;

namespace Hyperledger.Aries.Ledger
{
    /// <inheritdoc />
    public class DefaultLedgerService : ILedgerService
    {
        private readonly ILedgerSigningService _signingService;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultLedgerService" /> class
        /// </summary>
        /// <param name="signingService"></param>
        public DefaultLedgerService(ILedgerSigningService signingService)
        {
            _signingService = signingService;
        }

        /// <inheritdoc />
        public virtual async Task<AriesResponse> LookupDefinitionAsync(IAgentContext agentContext,
            string definitionId)
        {

            async Task<AriesResponse> LookupDefinition()
            {
                string req = await IndyLedger.BuildGetCredDefRequestAsync(null, definitionId);
                string res = await IndyLedger.SubmitRequestAsync((await agentContext.Pool).Pool, req);

                Indy.LedgerApi.ParseResponseResult result = await IndyLedger.ParseGetCredDefResponseAsync(res);
                return ConvertResult(result);
            }

            return await ResilienceUtils.RetryPolicyAsync(
                action: LookupDefinition,
                exceptionPredicate: (IndyException e) => e.SdkErrorCode == 309);
        }

        /// <inheritdoc />
        public virtual async Task<AriesResponse> LookupRevocationRegistryDefinitionAsync(IAgentContext agentContext,
            string registryId)
        {
            string req = await IndyLedger.BuildGetRevocRegDefRequestAsync(null, registryId);
            string res = await IndyLedger.SubmitRequestAsync((await agentContext.Pool).Pool, req);

            Indy.LedgerApi.ParseResponseResult result = await IndyLedger.ParseGetRevocRegDefResponseAsync(res);
            return ConvertResult(result);
        }

        /// <inheritdoc />
        public virtual async Task<AriesResponse> LookupSchemaAsync(IAgentContext agentContext, string schemaId)
        {
            async Task<AriesResponse> LookupSchema()
            {
                string req = await IndyLedger.BuildGetSchemaRequestAsync(null, schemaId);
                string res = await IndyLedger.SubmitRequestAsync((await agentContext.Pool).Pool, req);

                EnsureSuccessResponse(res);

                Indy.LedgerApi.ParseResponseResult result = await IndyLedger.ParseGetSchemaResponseAsync(res);
                return ConvertResult(result);
            }

            return await ResilienceUtils.RetryPolicyAsync(
                action: LookupSchema,
                exceptionPredicate: (IndyException e) => e.SdkErrorCode == 309);
        }

        /// <inheritdoc />
        public virtual async Task<AriesRegistryResponse> LookupRevocationRegistryDeltaAsync(IAgentContext agentContext, string revocationRegistryId,
             long from, long to)
        {
            string req = await IndyLedger.BuildGetRevocRegDeltaRequestAsync(null, revocationRegistryId, from, to);
            string res = await IndyLedger.SubmitRequestAsync((await agentContext.Pool).Pool, req);

            EnsureSuccessResponse(res);

            Indy.LedgerApi.ParseRegistryResponseResult result = await IndyLedger.ParseGetRevocRegDeltaResponseAsync(res);
            return ConvertResult(result);
        }

        /// <inheritdoc />
        public virtual async Task<AriesRegistryResponse> LookupRevocationRegistryAsync(IAgentContext agentContext, string revocationRegistryId,
             long timestamp)
        {
            string req = await IndyLedger.BuildGetRevocRegRequestAsync(null, revocationRegistryId, timestamp);
            string res = await IndyLedger.SubmitRequestAsync((await agentContext.Pool).Pool, req);

            EnsureSuccessResponse(res);

            Indy.LedgerApi.ParseRegistryResponseResult result = await IndyLedger.ParseGetRevocRegResponseAsync(res);
            return ConvertResult(result);
        }

        /// <inheritdoc />
        public virtual async Task RegisterSchemaAsync(IAgentContext context, string issuerDid, string schemaJson, TransactionCost paymentInfo = null)
        {
            string req = await IndyLedger.BuildSchemaRequestAsync(issuerDid, schemaJson);
            _ = await SignAndSubmitAsync(context, issuerDid, req, paymentInfo);
        }

        /// <inheritdoc />
        public virtual async Task<ServiceEndpointResult> LookupServiceEndpointAsync(IAgentContext context, string did)
        {
            string res = await LookupAttributeAsync(context, did, "endpoint");
            JObject jobj = JObject.Parse(res);

            return new ServiceEndpointResult { Result = jobj.ToObject<ServiceEndpointResult.ServiceEndpoint>() };
        }

        /// <inheritdoc />
        public virtual async Task RegisterServiceEndpointAsync(IAgentContext context, string did, string serviceEndpoint, TransactionCost paymentInfo = null)
        {
            var value = new { endpoint = serviceEndpoint };
            await RegisterAttributeAsync(context, did, did, "endpoint", value);
        }

        /// <inheritdoc />
        public virtual async Task RegisterCredentialDefinitionAsync(IAgentContext context, string submitterDid, string data, TransactionCost paymentInfo = null)
        {
            string req = await IndyLedger.BuildCredDefRequestAsync(submitterDid, data);
            _ = await SignAndSubmitAsync(context, submitterDid, req, paymentInfo);
        }

        /// <inheritdoc />
        public virtual async Task RegisterRevocationRegistryDefinitionAsync(IAgentContext context, string submitterDid,
            string data, TransactionCost paymentInfo = null)
        {
            string req = await IndyLedger.BuildRevocRegDefRequestAsync(submitterDid, data);
            _ = await SignAndSubmitAsync(context, submitterDid, req, paymentInfo);
        }

        /// <inheritdoc />
        public virtual async Task SendRevocationRegistryEntryAsync(IAgentContext context, string issuerDid,
            string revocationRegistryDefinitionId, string revocationDefinitionType, string value, TransactionCost paymentInfo = null)
        {
            string req = await IndyLedger.BuildRevocRegEntryRequestAsync(issuerDid, revocationRegistryDefinitionId,
                revocationDefinitionType, value);
            string res = await SignAndSubmitAsync(context, issuerDid, req, paymentInfo);

            EnsureSuccessResponse(res);
        }

        /// <inheritdoc />
        public virtual async Task RegisterNymAsync(IAgentContext context, string submitterDid, string theirDid,
            string theirVerkey, string role, TransactionCost paymentInfo = null)
        {
            if (DidUtils.IsFullVerkey(theirVerkey))
            {
                theirVerkey = await Did.AbbreviateVerkeyAsync(theirDid, theirVerkey);
            }

            string req = await IndyLedger.BuildNymRequestAsync(submitterDid, theirDid, theirVerkey, null, role);
            _ = await SignAndSubmitAsync(context, submitterDid, req, paymentInfo);
        }

        /// <inheritdoc />
        public virtual async Task<string> LookupAttributeAsync(IAgentContext agentContext, string targetDid, string attributeName)
        {
            string req = await IndyLedger.BuildGetAttribRequestAsync(null, targetDid, attributeName, null, null);
            string res = await IndyLedger.SubmitRequestAsync((await agentContext.Pool).Pool, req);

            string dataJson = JObject.Parse(res)["result"]!["data"]!.ToString();

            string attribute = JObject.Parse(dataJson)[attributeName]!.ToString();

            return attribute;
        }

        /// <inheritdoc />
        public virtual async Task<string> LookupTransactionAsync(IAgentContext agentContext, string ledgerType, int sequenceId)
        {
            string req = await IndyLedger.BuildGetTxnRequestAsync(null, ledgerType, sequenceId);
            string res = await IndyLedger.SubmitRequestAsync((await agentContext.Pool).Pool, req);

            return res;
        }

        /// <inheritdoc />
        public virtual async Task RegisterAttributeAsync(IAgentContext context, string submittedDid, string targetDid,
            string attributeName, object value, TransactionCost paymentInfo = null)
        {
            string data = $"{{\"{attributeName}\": {value.ToJson()}}}";

            string req = await IndyLedger.BuildAttribRequestAsync(submittedDid, targetDid, null, data, null);
            _ = await SignAndSubmitAsync(context, submittedDid, req, paymentInfo);
        }

        /// <inheritdoc />
        public virtual async Task<string> LookupNymAsync(IAgentContext agentContext, string did)
        {
            string req = await IndyLedger.BuildGetNymRequestAsync(null, did);
            string res = await IndyLedger.SubmitRequestAsync((await agentContext.Pool).Pool, req);

            EnsureSuccessResponse(res);

            return res;
        }

        /// <inheritdoc />
        public virtual async Task<IList<AuthorizationRule>> LookupAuthorizationRulesAsync(IAgentContext agentContext)
        {
            string req = await IndyLedger.BuildGetAuthRuleRequestAsync(null, null, null, null, null, null);
            string res = await IndyLedger.SubmitRequestAsync((await agentContext.Pool).Pool, req);

            EnsureSuccessResponse(res);

            JObject jobj = JObject.Parse(res);
            return jobj["result"]["data"].ToObject<IList<AuthorizationRule>>();
        }

        private async Task<string> SignAndSubmitAsync(IAgentContext agentContext, string submitterDid, string request, TransactionCost paymentInfo)
        {
            if (paymentInfo != null)
            {
                if (agentContext.AriesStorage.Wallet is null)
                {
                    throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Indy.WalletApi.Wallet)} which must not be null.");
                }

                Indy.PaymentsApi.PaymentResult requestWithFees = await IndyPayments.AddRequestFeesAsync(
                    wallet: agentContext.AriesStorage.Wallet,
                    submitterDid: null,
                    reqJson: request,
                    inputsJson: paymentInfo.PaymentAddress.Sources.Select(x => x.Source).ToJson(),
                    outputsJson: new[]
                    {
                        new IndyPaymentOutputSource
                        {
                            Recipient = paymentInfo.PaymentAddress.Address,
                            Amount = paymentInfo.PaymentAddress.Balance - paymentInfo.Amount
                        }
                    }.ToJson(),
                    extra: null);
                request = requestWithFees.Result;
            }
            string signedRequest = await _signingService.SignRequestAsync(agentContext, submitterDid, request);
            string response = await IndyLedger.SubmitRequestAsync((await agentContext.Pool).Pool, signedRequest);

            EnsureSuccessResponse(response);

            if (paymentInfo != null)
            {
                string responsePayment = await IndyPayments.ParseResponseWithFeesAsync(paymentInfo.PaymentMethod, response);
                IList<IndyPaymentOutputSource> paymentOutputs = responsePayment.ToObject<IList<IndyPaymentOutputSource>>();
                paymentInfo.PaymentAddress.Sources = paymentOutputs
                    .Where(x => x.Recipient == paymentInfo.PaymentAddress.Address)
                    .Select(x => new IndyPaymentInputSource
                    {
                        Amount = x.Amount,
                        PaymentAddress = x.Recipient,
                        Source = x.Receipt
                    })
                    .ToList();
            }
            return response;
        }

        private void EnsureSuccessResponse(string res)
        {
            JObject response = JObject.Parse(res);

            if (!response["op"].ToObject<string>().Equals("reply", StringComparison.OrdinalIgnoreCase))
            {
                throw new AriesFrameworkException(ErrorCode.LedgerOperationRejected, "Ledger operation rejected");
            }
        }

        private AriesResponse ConvertResult(Hyperledger.Indy.LedgerApi.ParseResponseResult result)
        {
            return new AriesResponse(result.Id, result.ObjectJson);
        }

        private AriesRegistryResponse ConvertResult(Hyperledger.Indy.LedgerApi.ParseRegistryResponseResult result)
        {
            return new AriesRegistryResponse(result.Id, result.ObjectJson, result.Timestamp);
        }

    }
}
