using Hyperledger.Aries.Storage;

namespace Hyperledger.Aries.Features.OpenID4Common.Records
{
    public class SdJwtCredentialRecord : RecordBase
    {
        public string CombinedIssuance { get; set; }
        
        public string KeyAlias { get; set; }
        public override string TypeName => "CredentialRecord.SdJwt";
    }
}
