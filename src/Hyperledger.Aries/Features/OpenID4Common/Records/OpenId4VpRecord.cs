using Hyperledger.Aries.Storage;

namespace Hyperledger.Aries.Features.OpenID4Common.Records
{
    public class OpenId4VpRecord : RecordBase
    {
        public string AuthenticationRequest { get; set; }
        public override string TypeName => "OpenId.VpRecord";
    }
}
