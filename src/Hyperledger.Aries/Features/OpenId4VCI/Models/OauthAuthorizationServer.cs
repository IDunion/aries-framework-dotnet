using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.Features.OpenId4VCI.Models
{

    public class OauthAuthorizationServer
    {
        [JsonProperty("issuer")]
        public string Issuer { get; set; }
        [JsonProperty("token_endpoint")]
        public string TokenEndpoint { get; set; }
        [JsonProperty("token_endpoint_auth_methods_supported")]
        public string[] TokenEndpointAuthMethodsSupported { get; set; }
        [JsonProperty("token_endpoint_auth_signing_alg_values_supported")]
        public string[] TokenEndpointAuthSigningAlgValuesSupported { get; set; }
        [JsonProperty("response_types_supported")]
        public string[] ResponseTypesSupported { get; set; }
    }

}
