using Newtonsoft.Json;

namespace Hyperledger.Aries.Features.OpenId4VCI.Models
{
    public class TokenResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }
        [JsonProperty("token_type")]
        public string TokenType { get; set; }
        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }
        [JsonProperty("c_nonce")]
        public string CNonce { get; set; }
        [JsonProperty("c_nonce_expires_in")]
        public int CNonceExpiresIn { get; set; }
    }


}
