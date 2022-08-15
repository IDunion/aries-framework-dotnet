using Hyperledger.Aries.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.Features.Handshakes.DidExchange
{
    internal class DidRecord : RecordBase
    {
        public override string TypeName => "AF.DidRecord";

        public string Did { get; set; }

        public string Verkey { get; set; }
    }
}
