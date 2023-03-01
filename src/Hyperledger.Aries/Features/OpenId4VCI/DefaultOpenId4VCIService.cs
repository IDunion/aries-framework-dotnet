using Flurl;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.IssueCredential;
using Hyperledger.Aries.Features.OpenID4Common.Records;
using Hyperledger.Aries.Features.OpenId4VCI.Models;
using Hyperledger.Aries.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Hyperledger.Aries.Features.OpenId4VCI
{
    public class DefaultOpenId4VCIService : IOpenId4VCIService
    {
        /// <summary>
        /// The record service
        /// </summary>
        protected readonly IWalletRecordService RecordService;

        /// <summary>
        /// The logger
        /// </summary>
        protected readonly ILogger<DefaultOpenId4VCIService> Logger;

        protected readonly HttpClient httpClient;

        public DefaultOpenId4VCIService(IWalletRecordService recordService, ILogger<DefaultOpenId4VCIService> logger)
        {
            httpClient = new HttpClient();
            RecordService = recordService;
            Logger = logger;
        }

        public async Task<SdJwtCredentialRecord> GetSdJwtCredentialAsnyc(IAgentContext agentContext, string recordId)
        {
            var record = await RecordService.GetAsync<SdJwtCredentialRecord>(agentContext.Wallet, recordId);

            if (record == null)
                throw new AriesFrameworkException(ErrorCode.RecordNotFound, "SdJwtCredentialRecord record not found");

            return record;
        }

        public async Task<OpenId4VciRecord> GetVciRecordAsnyc(IAgentContext agentContext, string recordId)
        {
            var record = await RecordService.GetAsync<OpenId4VciRecord>(agentContext.Wallet, recordId);

            if (record == null)
                throw new AriesFrameworkException(ErrorCode.RecordNotFound, "OpenId4VciRecord record not found");

            return record;
        }

        public Task<List<SdJwtCredentialRecord>> ListSdJwtCredentialAsync(IAgentContext agentContext, ISearchQuery query = null, int count = 100, int skip = 0)
        => RecordService.SearchAsync<SdJwtCredentialRecord>(agentContext.Wallet, query, null, count, skip);

        public Task<List<OpenId4VciRecord>> ListVciRecordAsync(IAgentContext agentContext, ISearchQuery query = null, int count = 100, int skip = 0)
        => RecordService.SearchAsync<OpenId4VciRecord>(agentContext.Wallet, query, null, count, skip);

        public CredOfferPayload ProcessCredentialOffer(string offer)
        {
            CredOfferPayload credOfferPayload = null;
            try
            {
                var decodedOffer = HttpUtility.UrlDecode(offer);
                var offerUri = new Uri(decodedOffer);
                string queryString = offerUri.Query;
                var queryDictionary = HttpUtility.ParseQueryString(queryString);

                string credOffer = "";
                if (queryDictionary.AllKeys.Contains("credential_offer"))
                {
                    credOffer = queryDictionary.Get("credential_offer");
                }
                if (!string.IsNullOrEmpty(credOffer))
                {
                    credOfferPayload = credOffer.ToObject<CredOfferPayload>();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"No CredentialOffer: {ex.Message}");
            }
            return credOfferPayload;
        }

        public async Task<TokenResponse> RequestToken(CredOfferPayload credOfferPayload)
        {
            var tokenValues = new Dictionary<string, string>
            {
                { "grant_type", "urn:ietf:params:oauth:grant-type:pre-authorized_code" },
                { "pre-authorized_code", credOfferPayload.Grants.GrantType.PreauthorizedCode }
            };
            var tokenData = new FormUrlEncodedContent(tokenValues);
            var tokenHttpResponse = await httpClient.PostAsync(Url.Combine(credOfferPayload.CredentialIssuer, "/token"), tokenData);
            var tokenResponseString = await tokenHttpResponse.Content.ReadAsStringAsync();

            TokenResponse tokenResponse = null;
            if (tokenHttpResponse.IsSuccessStatusCode)
            {
                tokenResponse = tokenResponseString.ToObject<TokenResponse>();
            }
            else
            {
                throw new Exception($"Status Code is {tokenHttpResponse.StatusCode} with message {tokenResponseString}");
            }

            return tokenResponse;
        }

        public async Task<CredResponse> RequestCredentials(CredOfferPayload credOfferPayload, TokenResponse tokenResponse)
        {
            CredRequest credRequest = new CredRequest
            {
                Format = "vc+sd-jwt",
                Type = "VerifiedEMail",
                Proof = new Proof
                {
                    ProofType = "jwt"
                }
            };

            // TODO: generate and insert credRequest.Proof.jwt

            var requestData = new StringContent(credRequest.ToJson(), Encoding.UTF8, "application/json");
            var credHttpResponse = await httpClient.PostAsync(Url.Combine(credOfferPayload.CredentialIssuer, "/credential"), requestData);
            var credResponseString = await credHttpResponse.Content.ReadAsStringAsync();

            CredResponse credResponse = null;
            if (credHttpResponse.IsSuccessStatusCode)
            {
                credResponse = credResponseString.ToObject<CredResponse>();
            }
            else
            {
                throw new Exception($"Status Code is {credHttpResponse.StatusCode} with message {credResponseString}");
            }

            return credResponse;
        }

        public async Task StoreSdJwtCredentialAsync(IAgentContext agentContext, SdJwtCredentialRecord sdJwtCredentialRecord)
        {
            var record = await RecordService.GetAsync<SdJwtCredentialRecord>(agentContext.Wallet, sdJwtCredentialRecord.Id);
            if (record == null)
            {
                await RecordService.AddAsync(agentContext.Wallet, sdJwtCredentialRecord);
            }
            else 
            {
                await RecordService.UpdateAsync(agentContext.Wallet, sdJwtCredentialRecord);
            }
        }

        public async Task StoreVciRecordAsync(IAgentContext agentContext, OpenId4VciRecord openId4VciRecord)
        {
            var record = await RecordService.GetAsync<SdJwtCredentialRecord>(agentContext.Wallet, openId4VciRecord.Id);
            if (record == null)
            {
                await RecordService.AddAsync(agentContext.Wallet, openId4VciRecord);
            }
            else
            {
                await RecordService.UpdateAsync(agentContext.Wallet, openId4VciRecord);
            }
        }
    }
}
