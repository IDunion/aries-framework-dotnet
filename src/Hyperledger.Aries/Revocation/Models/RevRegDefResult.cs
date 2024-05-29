using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;


namespace Hyperledger.Aries.Revocation.Models
{
    public class RevRegDefResult
    {
        public IntPtr Handle { get; set; }

        [JsonProperty]
        public string JobId { get; set; }

        [JsonProperty]
        public string revRegDefId { get { return this.revRegDefinitionState.revRegDefId; } }

        [JsonProperty]
        public RevRegDef revRegDef { get { return this.revRegDefinitionState.revRegDef; }  }
        [JsonProperty]
        public RevRegDefinitionState revRegDefinitionState { get; set; }
    }
}
