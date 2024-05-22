using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.Revocation.Models
{
    public class RevRegDefinitionState
    {
        [JsonProperty]
        public string revRegDefId { get; set; }

        [JsonProperty]
        public RevRegDef revRegDef { get; set; }

        [JsonProperty]
        public RevRegDefState revRegDefState { get; set; }


        public enum RevRegDefState
        {
            STATE_FINISHED = 0,
            STATE_FAILED = 1,
            STATE_ACTION = 2,
            STATE_WAIT = 3,
            STATE_DECOMMISSIONED = 4,
            STATE_FULL = 5
        }
        public static Dictionary<RevRegDefState,String> RevRegDefStateames = new Dictionary<RevRegDefState, String>(){
            { RevRegDefState.STATE_FINISHED , "finished" },
            { RevRegDefState.STATE_FAILED , "failed" },
            { RevRegDefState.STATE_ACTION , "action" },
            { RevRegDefState.STATE_WAIT , "wait" },
            { RevRegDefState.STATE_DECOMMISSIONED , "decommissioned" },
            { RevRegDefState.STATE_FULL , "full" }
            };
    }
}
