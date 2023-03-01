using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.Features.OpenId4VCI.Models
{
    public class CredRequest
    {
        [JsonProperty("format")]
        public string Format { get; set; }
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("proof")]
        public Proof Proof { get; set; }
    }

    public class Proof
    {
        [JsonProperty("proof_type")]
        public string ProofType { get; set; }
        [JsonProperty("jwt")]
        public string Jwt { get; set; }
    }

}
