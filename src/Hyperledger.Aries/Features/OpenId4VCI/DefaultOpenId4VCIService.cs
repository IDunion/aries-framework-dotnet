using Flurl;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.OpenID4Common.Records;
using Hyperledger.Aries.Features.OpenId4VCI.Models;
using Hyperledger.Aries.Storage;
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
            HttpClient httpClient = new HttpClient();

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

            var jwtBuilder = JwtBuilder.Create();
            jwtBuilder.Audience(credOfferPayload.CredentialIssuer);
            jwtBuilder.IssuedAt(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            jwtBuilder.AddClaim("nounce", tokenResponse.CNonce);

            var jwtAlg = new MockJwtAlgorithmFactory().CreateJwtAlgorithm("hackathon-key");
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

        public Task StoreSdJwtCredentialAsync(IAgentContext agentContext, SdJwtCredentialRecord sdJwtCredentialRecord)
        {
            throw new NotImplementedException();
        }

        public Task StoreVciRecordAsync(IAgentContext agentContext, OpenId4VciRecord openId4VciRecord)
        {
            throw new NotImplementedException();
        }
    }

    internal class MockJwtAlgorithmFactory : IJwtAlgorithmFactory
    {
        private const string jwk = "{\n    \"kty\": \"EC\",\n    \"d\": \"Iw6qWZhQ04CtijWzp3q-vGrQfmOcKd1SqjlxMgqzvwA\",\n    \"use\": \"sig\",\n    \"crv\": \"P-256\",\n    \"kid\": \"ECSNPzYd7TefqsBXX6LvfskkZSU=\",\n    \"x\": \"xYrl9sGkLv6_K5xa8jQK1ixQ8FC9pKlkzq2e2Po4_VY\",\n    \"y\": \"a281dDn0k54m0wKl-SfqkXLESv4_G8wZEQWpvKmfO2w\",\n    \"alg\": \"ES256\"\n}";

        public IJwtAlgorithm CreateJwtAlgorithm(string keyAlias)
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
