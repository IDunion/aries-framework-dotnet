using System;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.Revocation.Models
{
    public class RevRegDef
    {
        public string issuerId { get; set; }
        public string type { get; set; }
        public string credDefId { get; set; }
        public string Tag { get; set; }
        public RevRegDefValue value { get; set; }
    }
}
