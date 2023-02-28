using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.Features.OpenId4VCI.Models
{

    public class CredOfferPayload
    {
        [JsonProperty("credential_issuer")]
        public string CredentialIssuer { get; set; }
        [JsonProperty("credentials")]
        public Credential[] Credentials { get; set; }
        [JsonProperty("grants")]
        public Grants Grants { get; set; }
    }

    public class Grants
    {
        [JsonProperty("urn:ietf:params:oauth:grant-type:pre-authorized_code")]
        public GrantType GrantType { get; set; }
    }

    public class GrantType
    {
        [JsonProperty("pre-authorized_code")]
        public string PreauthorizedCode { get; set; }
        [JsonProperty("user_pin_required")]
        public bool UserPinRequired { get; set; }
    }

    public class Credential
    {
        [JsonProperty("format")]
        public string Format { get; set; }
        [JsonProperty("type")]
        public string Type { get; set; }
    }

}
