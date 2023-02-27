using Hyperledger.Aries.Features.OpenId4VerifiablePresentation.Models;
using Hyperledger.Aries.Storage;

namespace Hyperledger.Aries.Features.OpenId4VerifiablePresentation
{
    public class OpenId4VpRecord : RecordBase
    {
        // Todo: Define record attributes
        public AuthorizationRequest AuthorizationRequest { get; set; }
    }
}
