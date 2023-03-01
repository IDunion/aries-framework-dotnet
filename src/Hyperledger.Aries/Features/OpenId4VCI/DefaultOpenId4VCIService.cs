using Flurl;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.IssueCredential;
using Hyperledger.Aries.Features.OpenID4Common.Records;
using Hyperledger.Aries.Features.OpenId4VCI.Models;
using Hyperledger.Aries.Storage;
using Microsoft.Extensions.Logging;
using Hyperledger.Aries.Utils;
using JWT.Algorithms;
using JWT.Builder;
using SdJwt.Abstractions;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Hyperledger.Aries.Features.OpenId4VCI
{
    public class DefaultOpenId4VCIService : IOpenId4VCIService
    {
        private const string jwk = "{\n    \"kty\": \"EC\",\n    \"d\": \"Iw6qWZhQ04CtijWzp3q-vGrQfmOcKd1SqjlxMgqzvwA\",\n    \"use\": \"sig\",\n    \"crv\": \"P-256\",\n    \"kid\": \"ECSNPzYd7TefqsBXX6LvfskkZSU=\",\n    \"x\": \"xYrl9sGkLv6_K5xa8jQK1ixQ8FC9pKlkzq2e2Po4_VY\",\n    \"y\": \"a281dDn0k54m0wKl-SfqkXLESv4_G8wZEQWpvKmfO2w\",\n    \"alg\": \"ES256\"\n}";

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

            var jwtBuilder = JwtBuilder.Create();
            jwtBuilder.Audience(credOfferPayload.CredentialIssuer);
            jwtBuilder.IssuedAt(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            jwtBuilder.AddClaim("nonce", tokenResponse.CNonce);

            var jwtAlg = CreateJwtAlgorithm("hackathon-key");
            jwtBuilder.WithAlgorithm(jwtAlg);

            credRequest.Proof.Jwt = jwtBuilder.Encode();

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

        public async Task<OpenidCredentialIssuer> RequestOpenidCredentialIssuerData(CredOfferPayload credOfferPayload)
        {
            var credIssuerHttpResponse = await httpClient.GetAsync(Url.Combine(credOfferPayload.CredentialIssuer, "/.well-known/openid-credential-issuer"));
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

        private IJwtAlgorithm CreateJwtAlgorithm(string keyAlias)
        {
            // Todo: Use hardware key
            var jsonWebKey = new JsonWebKey(jwk);
            var x = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(jsonWebKey.X);
            var y = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(jsonWebKey.Y);
            var d = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(jsonWebKey.D);

            ECDsa ecdsa = ECDsa.Create(new ECParameters()
            {
                Curve = ECCurve.NamedCurves.nistP256,
                D = d,
                Q = new ECPoint { X = x, Y = y }
            })!;
            ECDsaSecurityKey key = new ECDsaSecurityKey(ecdsa);

            return new ES256Algorithm(key.ECDsa, key.ECDsa);
        }
    }
}
