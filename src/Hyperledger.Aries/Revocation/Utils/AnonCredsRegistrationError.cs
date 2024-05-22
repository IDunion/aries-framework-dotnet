using System;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.Revocation.Utils
{
    public class AnonCredsRegistrationError : Exception
    {
        public AnonCredsRegistrationError(string message) : base(message) { }
    }
}
