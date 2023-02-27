using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Features.OpenId4VerifiablePresentation.Helpers;
using Hyperledger.Aries.Features.OpenId4VerifiablePresentation.Models;
using Hyperledger.Aries.Features.OpenId4VerifiablePresentation.Models.PresentationExchange;
using Hyperledger.Aries.Features.PresentProof;
using Hyperledger.Aries.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hyperledger.Aries.Features.OpenId4VerifiablePresentation
{
    public class OpenId4VpClient : IOpenId4VpClient
    {
        private readonly HttpClient _httpClient;
        private readonly IWalletRecordService _recordService;
        
        public async Task<string> ProcessAuthenticationRequestUrl(IAgentContext agentContext, string url)
        {
            var uri = new Uri(url);
            AuthorizationRequest? authorizationRequest = null;
            if (uri.HasQueryParam("request_uri"))
            {
                var request = uri.GetQueryParam("request_uri");
                var response = await _httpClient.GetAsync(request);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    authorizationRequest = AuthorizationRequest.ParseFromJwt(content);
                }
            }
            else
            {
                authorizationRequest = AuthorizationRequest.ParseFromUri(uri);
            }
            
            if (authorizationRequest == null)
                throw new NullReferenceException("Unable to process OpenId url");
            
            var record = await ProcessAuthorizationRequestAsync(agentContext, authorizationRequest);
            
            return record.Id;
        }
        
        public async Task<string?> GenerateAuthenticationResponse(IAgentContext agentContext, string authRecordId, string credRecordId)
        {
            var authRecord = await _recordService.GetAsync<OpenId4VpRecord>(agentContext.Wallet, authRecordId);
            var credRecord = await _recordService.GetAsync<SdJwtRecord>(agentContext.Wallet, authRecordId);

            // Todo: Get AuthorizationRequest from OpenId4VPRecord
            var authenticationRequest = new AuthorizationRequest();

            var vpToken = CreateVpToken(credRecord);
            // Todo: Create presentation submission dynamically
            var presentationSubmission = CreateStaticPresentationSubmission(authenticationRequest);
            if (authenticationRequest?.ResponseMode == "direct_post")
            {
                await SendAuthorizationResponse(authenticationRequest, vpToken, JsonConvert.SerializeObject(presentationSubmission));
                return null;
            }
            else
            {
                var callbackUrl = PrepareAuthorizationResponse(authenticationRequest!, vpToken,
                    JsonConvert.SerializeObject(presentationSubmission));
                    
                return callbackUrl;
            }
        }

        public Task<OpenId4VpRecord> ProcessAuthorizationRequestAsync(IAgentContext agentContext, AuthorizationRequest authorizationRequest)
        {
            throw new NotImplementedException();
        }

        public string PrepareAuthorizationResponse(AuthorizationRequest authorizationRequest, string vpToken, string presentation_submission)
        {
            var redirectUri = new Uri(authorizationRequest.RedirectUri);

            var callbackUri = new UriBuilder();
            callbackUri.Scheme = redirectUri.Scheme;
            callbackUri.Host = redirectUri.Host;
            callbackUri.Port = redirectUri.Port;
            callbackUri.Path = redirectUri.PathAndQuery.Contains("?") ? redirectUri.PathAndQuery.Split("?").First() : redirectUri.PathAndQuery;
            callbackUri.Query = $"response_type=vp_token&presentation_submission={presentation_submission}&vp_token={vpToken}";

            return Uri.EscapeUriString(callbackUri.Uri.ToString());
        }
        
        private async Task SendAuthorizationResponse(AuthorizationRequest authorizationRequest, string vpToken, string presentationSubmission)
        {
            var content = new List<KeyValuePair<string, string>>();
            content.Add(new KeyValuePair<string, string>("vp_token", vpToken));
            content.Add(new KeyValuePair<string, string>("presentation_submission", presentationSubmission));
            
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(authorizationRequest.RedirectUri),
                Method = HttpMethod.Post,
                Content = new FormUrlEncodedContent(content)
            };

            await _httpClient.SendAsync(request);
        }
        
        private string CreateVpToken(ProofRecord proofRecord)
        {
            var proofJObject = JObject.Parse(proofRecord.ProofJson);
            return proofJObject.ToString(Formatting.None);
        }
        
        public static object CreateStaticPresentationSubmission(AuthorizationRequest authRequest)
        {
            var request = PresentationDefinition.FromJson(authRequest.PresentationDefinition);
            
            return new
            {
                id = "NextcloudCredentialPresentationSubmission",
                definition_id = request.Id,
                descriptor_map = new[]
                {
                    new
                    {
                        id = "NextcloudCredentialAC",
                        format = "ac_vp",
                        path = "$",
                        path_nested = new
                        {
                            id = "NextcloudCredentialAC",
                            path = "$.requested_proof.revealed_attr_groups.NextcloudCredentialAC",
                            format = "ac_vp"
                        }
                    }
                }
            };
        }
    }
}
