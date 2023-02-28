using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Features.OpenID4Common.Events;
using Hyperledger.Aries.Features.OpenID4Common.Records;
using Hyperledger.Aries.Features.OpenId4VerifiablePresentation.Helpers;
using Hyperledger.Aries.Features.OpenId4VerifiablePresentation.Models;
using Hyperledger.Aries.Features.OpenId4VerifiablePresentation.Models.PresentationExchange;
using Hyperledger.Aries.Features.PresentProof;
using Hyperledger.Aries.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SdJwt.Abstractions;
using SdJwt.Models;

namespace Hyperledger.Aries.Features.OpenId4VerifiablePresentation
{
    public class OpenId4VpClient : IOpenId4VpClient
    {
        public OpenId4VpClient(
            IWalletRecordService recordService,
            IEventAggregator eventAggregator)
        {
            _recordService = recordService;
            _eventAggregator = eventAggregator;
            _httpClient = new HttpClient();
        }
        
        private readonly HttpClient _httpClient;
        private readonly IWalletRecordService _recordService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IHolder _holder;
        
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
                    //authorizationRequest = content;
                }
            }
            else
            {
                authorizationRequest = AuthorizationRequest.ParseFromUri(uri);
                //authorizationRequest = uri.ToString();
            }
            
            if (authorizationRequest == null)
                throw new NullReferenceException("Unable to process OpenId url");

            var record = new OpenId4VpRecord
            {
                PresentationDefinition = authorizationRequest.PresentationDefinition,
                ResponseMode = authorizationRequest.ResponseMode,
                Nonce = authorizationRequest.Nonce,
                RedirectUri = authorizationRequest.RedirectUri
            };
            
            await _recordService.AddAsync(agentContext.Wallet, record);
            
            _eventAggregator.Publish(new NewPresentationRequestEvent()
            {
                RecordId = record.Id
            });

            return record.Id;
        }
        
        public async Task<string?> GenerateAuthenticationResponse(IAgentContext agentContext, string authRecordId, string credRecordId)
        {
            var authRecord = await _recordService.GetAsync<OpenId4VpRecord>(agentContext.Wallet, authRecordId);
            var sdJwtRecord = await _recordService.GetAsync<SdJwtCredentialRecord>(agentContext.Wallet, authRecordId);

            //var authenticationRequest = authRecord.AuthenticationRequest;

            var vpToken = CreateVpToken(sdJwtRecord, authRecord);
            // Todo: Create presentation submission dynamically
            //var presentationSubmission = CreateStaticPresentationSubmission(authenticationRequest);
            var presentationSubmission = CreatePresentationSubmission(authRecord);
            if (authRecord.ResponseMode == "direct_post")
            {
                await SendAuthorizationResponse(authRecord, vpToken, JsonConvert.SerializeObject(presentationSubmission));
                return null;
            }
            else
            {
                var callbackUrl = PrepareAuthorizationResponse(authRecord!, vpToken,
                    JsonConvert.SerializeObject(presentationSubmission));
                    
                return callbackUrl;
            }
        }

        public Task<OpenId4VpRecord> ProcessAuthorizationRequestAsync(IAgentContext agentContext, AuthorizationRequest authorizationRequest)
        {
            throw new NotImplementedException();
            //return new OpenId4VpRecord().AuthenticationRequest = authorizationRequest;
        }

        public string PrepareAuthorizationResponse(OpenId4VpRecord authorizationRequest, object vpToken, string presentation_submission)
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
        
        private async Task SendAuthorizationResponse(OpenId4VpRecord authorizationRequest, object vpToken, string presentationSubmission)
        {
            var content = new List<KeyValuePair<string, string>>();
            content.Add(new KeyValuePair<string, string>("vp_token", vpToken.ToString()));
            content.Add(new KeyValuePair<string, string>("presentation_submission", presentationSubmission));
            
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(authorizationRequest.RedirectUri),
                Method = HttpMethod.Post,
                Content = new FormUrlEncodedContent(content)
            };

            await _httpClient.SendAsync(request);
        }
        
        private object CreateVpToken(SdJwtCredentialRecord sdJwtRecord, OpenId4VpRecord vpRecord)
        {
            var sdJwtDoc = new SdJwtDoc(sdJwtRecord.CombinedIssuance);
            _holder.CreatePresentation(sdJwtDoc);
        }
        
        public static object CreatePresentationSubmission(OpenId4VpRecord authRecord)
        {
            return new
            {
                id = "NexcloudCredentialPresentationSubmission",
                //id holen
                definition_id = authRecord.PresentationDefinition,
                descriptor_map = new[]
                {
                    new
                    {
                        id = "VerifiedEMail",
                        format = "verifiable-credential+sd-jwt",
                        path = "$"
                    }
                }
            };
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
                        id = "VerifiableEmail",
                        format = "ac_vp",
                        path = "$",
                        path_nested = new
                        {
                            id = "VerifiableEmail",
                            path = "$.requested_proof.revealed_attr_groups.NextcloudCredentialAC",
                            format = "ac_vp"
                        }
                    }
                }
            };
        }
    }
}
