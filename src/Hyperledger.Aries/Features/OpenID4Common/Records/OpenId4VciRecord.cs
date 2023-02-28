using Hyperledger.Aries.Storage;

namespace Hyperledger.Aries.Features.OpenID4Common.Records
{
    public class OpenId4VciRecord : RecordBase
    {
        public string CredentialMetadata { get; set; }
        
        public string CredentialOfferPayload { get; set; }
        
        public string CredentialRecordId { get; set; }
        public override string TypeName => "OpenID.VciRecord";
    }
}
