using Hyperledger.Aries.Features.OpenId4VCI.Models;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Features.OpenId4VCI
{
    public interface IOpenId4VCIService
    {
        public CredOfferPayload ProcessCredentialOffer(string offer);
        public Task<TokenResponse> RequestToken(CredOfferPayload credOfferPayload);
        public Task<CredResponse> RequestCredentials(CredOfferPayload credOfferPayload, TokenResponse tokenResponse);
    }
}
