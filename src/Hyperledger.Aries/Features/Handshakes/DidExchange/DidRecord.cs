using Hyperledger.Aries.Storage.Records;

namespace Hyperledger.Aries.Features.Handshakes.DidExchange
{
    public class DidRecord : RecordBase
    {
        public override string TypeName => "AF.DidRecord";

        public string Did { get; set; }

        public string Verkey { get; set; }
    }
}
