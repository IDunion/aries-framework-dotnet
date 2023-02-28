using Hyperledger.Aries.Features.OpenId4VCI.Models;

namespace Hyperledger.Aries.Features.OpenId4VCI
{
    public interface IOpenId4VCIService
    {
        public CredOfferPayload ProcessCredentialOffer(string offer);
    }
}
