using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Flurl;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.OpenID4Common.Records;
using Hyperledger.Aries.Features.OpenId4VCI.Models;
using Hyperledger.Aries.Storage;
using JWT.Builder;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SdJwt.Abstractions;

namespace Hyperledger.Aries.Features.OpenId4VCI
{
    public class DefaultOpenId4VCIService : IOpenId4VCIService
    {
        protected readonly IWalletRecordService RecordService;
        protected readonly ILogger<DefaultOpenId4VCIService> Logger;
        protected readonly IHttpClientFactory HttpClientFactory;
        protected readonly IJwtSigningAlgorithmFactory JwtSigningAlgorithmFactory;

        public DefaultOpenId4VCIService(IWalletRecordService recordService, ILogger<DefaultOpenId4VCIService> logger, IHttpClientFactory httpClientFactory, IJwtSigningAlgorithmFactory jwtSigningAlgorithmFactory)
        {
            RecordService = recordService;
            Logger = logger;
            HttpClientFactory = httpClientFactory;
            JwtSigningAlgorithmFactory = jwtSigningAlgorithmFactory;
        }

        public async Task<OpenId4VciRecord> ProcessCredentialOfferAsync(IAgentContext context, string offer)
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
                var credOfferPayload = credOffer.ToObject<CredOfferPayload>();
                
                var record = new OpenId4VciRecord()
                {
                    Id = Guid.NewGuid().ToString(), 
                    CredentialOfferPayload = credOfferPayload
                };
                
                var credentialIssuerMetadata = await RequestOpenidCredentialIssuerData(credOfferPayload);
                record.CredentialMetadata = credentialIssuerMetadata;

                await StoreVciRecordAsync(context, record);
                return record;
            }
            
            throw new InvalidOperationException($"No CredentialOffer: {offer}");
        }

        public async Task<OpenId4VciRecord> RequestCredentialAsync(IAgentContext context, string recordId, string keyRefId, string userPin = null)
        {
            var vciRecord = await GetVciRecordAsnyc(context, recordId);
            
            var oauthResponse = await RequestOauthAuthorizationServer(vciRecord.CredentialOfferPayload);
            var token = await RequestToken(vciRecord.CredentialOfferPayload, oauthResponse);
            
            var credResponse = await RequestCredentials(vciRecord.CredentialOfferPayload, token);
            
            var sdJwtRecord = new SdJwtCredentialRecord
            {
                Id = Guid.NewGuid().ToString(),
                CombinedIssuance = credResponse.Credential,
                KeyAlias = keyRefId
            };
            
            await StoreSdJwtCredentialAsync(context, sdJwtRecord);
            vciRecord.CredentialRecordId = sdJwtRecord.Id;
            await StoreVciRecordAsync(context, vciRecord);

            return vciRecord;
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
        
        private async Task<TokenResponse> RequestToken(CredOfferPayload credOfferPayload, OauthAuthorizationServer oauthAuthorizationServer)
        {
            var tokenValues = new Dictionary<string, string>
            {
                { "grant_type", "urn:ietf:params:oauth:grant-type:pre-authorized_code" },
                { "pre-authorized_code", credOfferPayload.Grants.GrantType.PreauthorizedCode }
            };
            var tokenData = new FormUrlEncodedContent(tokenValues);
            var tokenHttpResponse = await HttpClientFactory.CreateClient().PostAsync(oauthAuthorizationServer.TokenEndpoint, tokenData);
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

        private async Task<CredResponse> RequestCredentials(CredOfferPayload credOfferPayload, TokenResponse tokenResponse)
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

            // Todo: Make key alias dynamic
            var id = "test";
            var jwt = CreateProofOfPossession(id, tokenResponse.CNonce, credOfferPayload.CredentialIssuer);
            // Todo: Get proof jwt signed by JwtSigningAlgorithmFactory
            credRequest.Proof.Jwt = jwt;

            var requestData = new StringContent(credRequest.ToJson(), Encoding.UTF8, "application/json");

            HttpResponseMessage credHttpResponse;
            using (var httpClientWithAuth = new HttpClient())
            {
                httpClientWithAuth.DefaultRequestHeaders.Add("Authorization", tokenResponse.TokenType + " " + tokenResponse.AccessToken);
                credHttpResponse = await httpClientWithAuth.PostAsync(Url.Combine(credOfferPayload.CredentialIssuer, "/credential"), requestData);
            }
            
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

        private string CreateProofOfPossession(string keyAlias, string nonce, string audience)
        {
            var jwtSigner = JwtSigningAlgorithmFactory.CreateAlgorithm(keyAlias);

            JObject jwk = JObject.Parse(jwtSigner.GetJwk());

            var builder = JwtBuilder.Create()
                .AddHeader(HeaderName.Type, "openid4vci-proof+jwt")
                .AddHeader("jwk", jwk)
                .AddClaim("nonce", nonce)
                .AddClaim("aud", audience)
                .AddClaim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            builder.WithAlgorithm(jwtSigner);
            builder.WithSecret("");

            return builder.Encode();
        }

        private async Task<OpenidCredentialIssuer> RequestOpenidCredentialIssuerData(CredOfferPayload credOfferPayload)
        {
            var credIssuerHttpResponse = await HttpClientFactory.CreateClient().GetAsync(Url.Combine(credOfferPayload.CredentialIssuer, "/.well-known/openid-credential-issuer"));
            var credIsseuerResponseString = await credIssuerHttpResponse.Content.ReadAsStringAsync();

            if (!credIssuerHttpResponse.IsSuccessStatusCode)
            {
                throw new Exception($"Status Code is {credIssuerHttpResponse.StatusCode} with message {credIsseuerResponseString}");
            }
            try
            {
                return credIsseuerResponseString.ToObject<OpenidCredentialIssuer>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error parsing the result as OpenidCredentialIssuer with message {ex.Message}");
            }
        }

        private async Task<OauthAuthorizationServer> RequestOauthAuthorizationServer(CredOfferPayload credOfferPayload)
        {
            var credIssuerHttpResponse = await HttpClientFactory.CreateClient().GetAsync(Url.Combine(credOfferPayload.CredentialIssuer, "/.well-known/oauth-authorization-server"));
            var credIsseuerResponseString = await credIssuerHttpResponse.Content.ReadAsStringAsync();

            if (!credIssuerHttpResponse.IsSuccessStatusCode)
            {
                throw new Exception($"Status Code is {credIssuerHttpResponse.StatusCode} with message {credIsseuerResponseString}");
            }
            try
            {
                return credIsseuerResponseString.ToObject<OauthAuthorizationServer>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error parsing the result as OpenidCredentialIssuer with message {ex.Message}");
            }
        }
    }
}
