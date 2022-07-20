﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Ledger.Models;
using Hyperledger.Aries.Payments;
using Hyperledger.Aries.Utils;
using Hyperledger.Indy;
using Hyperledger.Indy.DidApi;
using Hyperledger.Indy.LedgerApi;
using Newtonsoft.Json.Linq;
using IndyPayments = Hyperledger.Indy.PaymentsApi.Payments;
using IndyVdrPool = indy_vdr_dotnet.libindy_vdr.PoolApi;
using IndyVdrLedger = indy_vdr_dotnet.libindy_vdr.LedgerApi;
using IndyVdrRequest = indy_vdr_dotnet.libindy_vdr.RequestApi;

namespace Hyperledger.Aries.Ledger
{
    /// <inheritdoc />
    public class NewLedgerService : INewLedgerService
    {
        private readonly INewLedgerSigningService _signingService;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultLedgerService" /> class
        /// </summary>
        /// <param name="signingService"></param>
        public NewLedgerService(INewLedgerSigningService signingService)
        {
            _signingService = signingService;
        }

        /// <inheritdoc />
        public virtual async Task<ParseResponseResult> LookupDefinitionAsync(INewAgentContext agentContext,
            string definitionId)
        {
            async Task<ParseResponseResult> LookupDefinition()
            {
                var req = await IndyVdrLedger.BuildGetCredDefRequest(definitionId);
                var res = await IndyVdrPool.SubmitPoolRequestAsync(await agentContext.Pool, req);

                return await IndyVdrLedger.ParseGetCredDefResponseAsync(res);
            }

            return await ResilienceUtils.RetryPolicyAsync(
                action: LookupDefinition,
                exceptionPredicate: (IndyException e) => e.SdkErrorCode == 309);
        }

        /// <inheritdoc />
        public virtual async Task<ParseResponseResult> LookupRevocationRegistryDefinitionAsync(INewAgentContext agentContext,
            string registryId)
        {
            var req = await IndyVdrLedger.BuildGetRevocRegDefRequest(registryId);
            var res = await IndyVdrPool.SubmitPoolRequestAsync(await agentContext.Pool, req);

            return await IndyVdrLedger.ParseGetRevocRegDefResponseAsync(res);
        }

        /// <inheritdoc />
        public virtual async Task<ParseResponseResult> LookupSchemaAsync(INewAgentContext agentContext, string schemaId)
        {
            async Task<ParseResponseResult> LookupSchema()
            {
                var req = await IndyVdrLedger.BuildGetSchemaRequestAsync(schemaId);
                var res = await IndyVdrPool.SubmitPoolRequestAsync(await agentContext.Pool, req);

                EnsureSuccessResponse(res);

                return await IndyVdrLedger.ParseGetSchemaResponseAsync(res);
            };

            return await ResilienceUtils.RetryPolicyAsync(
                action: LookupSchema,
                exceptionPredicate: (IndyException e) => e.SdkErrorCode == 309);
        }

        /// <inheritdoc />
        public virtual async Task<ParseRegistryResponseResult> LookupRevocationRegistryDeltaAsync(INewAgentContext agentContext, string revocationRegistryId,
             long from, long to)
        {
            var req = await IndyVdrLedger.BuildGetRevocRegDeltaRequestAsync(revocationRegistryId, to, from);
            var res = await IndyVdrPool.SubmitPoolRequestAsync(await agentContext.Pool, req);

            EnsureSuccessResponse(res);

            return await IndyVdrLedger.ParseGetRevocRegDeltaResponseAsync(res);
        }

        /// <inheritdoc />
        public virtual async Task<ParseRegistryResponseResult> LookupRevocationRegistryAsync(INewAgentContext agentContext, string revocationRegistryId,
             long timestamp)
        {
            var req = await IndyVdrLedger.BuildGetRevocRegRequest(revocationRegistryId, timestamp);
            var res = await IndyVdrPool.SubmitPoolRequestAsync(await agentContext.Pool, req);

            EnsureSuccessResponse(res);

            return await IndyVdrLedger.ParseGetRevocRegResponseAsync(res);
        }

        /// <inheritdoc />
        public virtual async Task RegisterSchemaAsync(INewAgentContext context, string issuerDid, string schemaJson, TransactionCost paymentInfo = null)
        {
            var req = await IndyVdrRequest.RequestGetBodyAsync(await IndyVdrLedger.BuildSchemaRequestAsync(issuerDid, schemaJson));
            _ = await SignAndSubmitAsync(context, issuerDid, req, paymentInfo);
        }

        /// <inheritdoc />
        public async Task<ServiceEndpointResult> LookupServiceEndpointAsync(INewAgentContext context, string did)
        {
            var res = await LookupAttributeAsync(context, did, "endpoint");

            var jobj = JObject.Parse(res);
            var endpoint = jobj["result"]?["data"]?.ToString();

            return !string.IsNullOrEmpty(endpoint) ? JObject.Parse(endpoint).ToObject<ServiceEndpointResult>() : null;
        }

        /// <inheritdoc />
        public async Task RegisterServiceEndpointAsync(INewAgentContext context, string did, string serviceEndpoint, TransactionCost paymentInfo = null)
        {
            var value = new { endpoint = serviceEndpoint };
            await RegisterAttributeAsync(context, did, did, "endpoint", value);
        }

        /// <inheritdoc />
        public virtual async Task RegisterCredentialDefinitionAsync(INewAgentContext context, string submitterDid, string data, TransactionCost paymentInfo = null)
        {
            var req = await IndyVdrRequest.RequestGetBodyAsync(await IndyVdrLedger.BuildCredDefRequest(submitterDid, data));
            _ = await SignAndSubmitAsync(context, submitterDid, req, paymentInfo);
        }

        /// <inheritdoc />
        public virtual async Task RegisterRevocationRegistryDefinitionAsync(INewAgentContext context, string submitterDid,
            string data, TransactionCost paymentInfo = null)
        {
            var req = await IndyVdrRequest.RequestGetBodyAsync(await IndyVdrLedger.BuildRevocRegDefRequestAsync(submitterDid, data));
            _ = await SignAndSubmitAsync(context, submitterDid, req, paymentInfo);
        }

        /// <inheritdoc />
        public virtual async Task SendRevocationRegistryEntryAsync(INewAgentContext context, string issuerDid,
            string revocationRegistryDefinitionId, string revocationDefinitionType, string value, TransactionCost paymentInfo = null)
        {
            var req = await IndyVdrRequest.RequestGetBodyAsync(
                await IndyVdrLedger.BuildRevocRegEntryRequestAsync(issuerDid, revocationRegistryDefinitionId,revocationDefinitionType, value)
                );
            var res = await SignAndSubmitAsync(context, issuerDid, req, paymentInfo);

            EnsureSuccessResponse(res);
        }

        /// <inheritdoc />
        public virtual async Task RegisterNymAsync(INewAgentContext context, string submitterDid, string theirDid,
            string theirVerkey, string role, TransactionCost paymentInfo = null)
        {
            if (DidUtils.IsFullVerkey(theirVerkey))
                theirVerkey = await Did.AbbreviateVerkeyAsync(theirDid, theirVerkey);

            var req = await IndyVdrRequest.RequestGetBodyAsync(await IndyVdrLedger.BuildNymRequestAsync(submitterDid, theirDid, theirVerkey, null, role));
            _ = await SignAndSubmitAsync(context, submitterDid, req, paymentInfo);
        }

        /// <inheritdoc />
        public virtual async Task<string> LookupAttributeAsync(INewAgentContext agentContext, string targetDid, string attributeName)
        {
            var req = await IndyVdrLedger.BuildGetAttributeRequest(targetDid, null, null, attributeName, null);
            var res = await IndyVdrPool.SubmitPoolRequestAsync(await agentContext.Pool, req);

            return res;
        }

        /// <inheritdoc />
        public virtual async Task<string> LookupTransactionAsync(INewAgentContext agentContext, string ledgerType, int sequenceId)
        {
            if(int.TryParse(ledgerType, out int ledgerTypeAsInt) == false)
            {
                //Throw exception
            };
            var req = await IndyVdrLedger.BuildGetTxnRequestAsync(ledgerTypeAsInt, sequenceId);
            var res = await IndyVdrPool.SubmitPoolRequestAsync(await agentContext.Pool, req);

            return res;
        }

        /// <inheritdoc />
        public virtual async Task RegisterAttributeAsync(INewAgentContext context, string submittedDid, string targetDid,
            string attributeName, object value, TransactionCost paymentInfo = null)
        {
            var data = $"{{\"{attributeName}\": {value.ToJson()}}}";

            var req = await IndyVdrRequest.RequestGetBodyAsync(await IndyVdrLedger.BuildAttributeRequest(targetDid, submittedDid, null, data, null));
            _ = await SignAndSubmitAsync(context, submittedDid, req, paymentInfo);
        }

        /// <inheritdoc />
        public async Task<string> LookupNymAsync(INewAgentContext agentContext, string did)
        {
            var req = await IndyVdrLedger.BuildGetNymRequest(did);
            var res = await IndyVdrPool.SubmitPoolRequestAsync(await agentContext.Pool, req);

            EnsureSuccessResponse(res);

            return res;
        }

        /// <inheritdoc />
        public async Task<IList<AuthorizationRule>> LookupAuthorizationRulesAsync(INewAgentContext agentContext)
        {
            var req = await IndyVdrLedger.BuildGetAuthRuleRequestAsync(null, null, null, null, null, null);
            var res = await IndyVdrPool.SubmitPoolRequestAsync(await agentContext.Pool, req);

            EnsureSuccessResponse(res);

            var jobj = JObject.Parse(res);
            return jobj["result"]["data"].ToObject<IList<AuthorizationRule>>();
        }

        private async Task<string> SignAndSubmitAsync(INewAgentContext context, string submitterDid, string request, TransactionCost paymentInfo)
        {
            if (paymentInfo != null)
            {
                var requestWithFees = await IndyPayments.AddRequestFeesAsync(
                    wallet: context.Wallet,
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
            var signedRequest = await _signingService.SignRequestAsync(context, submitterDid, request);
            var response = await IndyVdrPool.SubmitPoolRequestAsync(await context.Pool, await IndyVdrLedger.BuildCustomRequest(signedRequest));

            EnsureSuccessResponse(response);

            if (paymentInfo != null)
            {
                var responsePayment = await IndyPayments.ParseResponseWithFeesAsync(paymentInfo.PaymentMethod, response);
                var paymentOutputs = responsePayment.ToObject<IList<IndyPaymentOutputSource>>();
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

        void EnsureSuccessResponse(string res)
        {
            var response = JObject.Parse(res);

            if (!response["op"].ToObject<string>().Equals("reply", StringComparison.OrdinalIgnoreCase))
                throw new AriesFrameworkException(ErrorCode.LedgerOperationRejected, "Ledger operation rejected");
        }
    }
}
