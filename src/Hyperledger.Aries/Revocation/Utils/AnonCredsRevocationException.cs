using System;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.Revocation.Utils
{
    public class AnonCredsRevocationException : Exception
    {
        public AnonCredsRevocationException(string message) : base(message) { }
    }
}
