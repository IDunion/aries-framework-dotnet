using Hyperledger.Aries.Storage;

namespace Hyperledger.Aries.Features.Handshakes.DidExchange
{
    internal class KeyRecord : RecordBase
    {
        public override string TypeName => "AF.KeyRecord";

        public string Signkey { get; set; }

        public string Verkey { get; set; }
    }
}
