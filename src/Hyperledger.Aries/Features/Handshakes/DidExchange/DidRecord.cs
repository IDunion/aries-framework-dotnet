using Hyperledger.Aries.Storage.Records;

namespace Hyperledger.Aries.Features.Handshakes.DidExchange
{
    public class DidRecord : RecordBase
    {

        //TODO : ??? - Maybe add more info like didmethod, verkey_type, metadata, (see acapy implementation -> create_local_did and more)
        // Also add Tags where dids are created? -> fetch_all via tagfilter possible
        public override string TypeName => "AF.DidRecord";

        public string Did { get; set; }

        public string Verkey { get; set; }
    }
}
