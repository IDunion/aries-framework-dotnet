using System;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.Revocation.Models
{
    public class RevRegDefValue
    {
        public Dictionary<string, object> PublicKeys { get; set; }
        public int MaxCredNum { get; set; }
        public string TailsLocation { get; set; }
        public string TailsHash { get; set; }

    }
}
