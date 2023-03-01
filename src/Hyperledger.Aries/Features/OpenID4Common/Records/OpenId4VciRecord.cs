using Hyperledger.Aries.Features.OpenId4VCI.Models;
using Hyperledger.Aries.Storage;

namespace Hyperledger.Aries.Features.OpenID4Common.Records
{
    public class OpenId4VciRecord : RecordBase
    {
        public OpenidCredentialIssuer CredentialMetadata { get; set; }
        
        public CredOfferPayload CredentialOfferPayload { get; set; }
        
        public string CredentialRecordId { get; set; }

        public override string TypeName => "OpenID.VciRecord";
    }
}
