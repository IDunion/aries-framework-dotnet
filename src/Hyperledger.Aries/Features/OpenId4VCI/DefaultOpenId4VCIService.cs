using Flurl;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.OpenID4Common.Records;
using Hyperledger.Aries.Features.OpenId4VCI.Models;
using Hyperledger.Aries.Storage;
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
        protected readonly HttpClient httpClient;

        public DefaultOpenId4VCIService()
        {
            httpClient = new HttpClient();
        }

        public Task<SdJwtCredentialRecord> GetSdJwtCredentialAsnyc(IAgentContext agentContext, string recordId)
        {
            throw new NotImplementedException();
        }

        public Task<OpenId4VciRecord> GetVciRecordAsnyc(IAgentContext agentContext, string recordId)
        {
            throw new NotImplementedException();
        }

        public Task<List<SdJwtCredentialRecord>> ListSdJwtCredentialAsync(IAgentContext agentContext, ISearchQuery query = null, int count = 100, int skip = 0)
        {
            throw new NotImplementedException();
        }

        public Task<List<OpenId4VciRecord>> ListVciRecordAsync(IAgentContext agentContext, ISearchQuery query = null, int count = 100, int skip = 0)
        {
            throw new NotImplementedException();
        }

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
            HttpClient httpClient = new HttpClient();
            
            CredRequest credRequest = new CredRequest();
            credRequest.Format = "vc+sd-jwt";
            credRequest.Type = "VerifiedEMail";
            credRequest.Proof = new Proof();
            credRequest.Proof.ProofType = "jwt";

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

        public Task StoreSdJwtCredentialAsync(IAgentContext agentContext, SdJwtCredentialRecord sdJwtCredentialRecord)
        {
            throw new NotImplementedException();
        }

        public Task StoreVciRecordAsync(IAgentContext agentContext, OpenId4VciRecord openId4VciRecord)
        {
            throw new NotImplementedException();
        }
    }
}
