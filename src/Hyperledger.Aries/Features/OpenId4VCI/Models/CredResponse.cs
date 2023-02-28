using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.Features.OpenId4VCI.Models
{
    public class CredResponse
    {
        [JsonProperty("format")]
        public string Format { get; set; }
        [JsonProperty("credential")]
        public string Credential { get; set; }
    }

}
