using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Features.IssueCredential;
using Hyperledger.Aries.Features.OpenID4Common.Records;
using Hyperledger.Aries.Features.OpenId4VCI.Models;
using Hyperledger.Aries.Storage;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Features.OpenId4VCI
{
    public interface IOpenId4VCIService
    {
        CredOfferPayload ProcessCredentialOffer(string offer);
        Task<OpenidCredentialIssuer> RequestOpenidCredentialIssuerData(CredOfferPayload credOfferPayload);
        Task<TokenResponse> RequestToken(CredOfferPayload credOfferPayload);
        Task<CredResponse> RequestCredentials(CredOfferPayload credOfferPayload, TokenResponse tokenResponse);
        Task<SdJwtCredentialRecord> GetSdJwtCredentialAsnyc(IAgentContext agentContext, string recordId);
        Task<List<SdJwtCredentialRecord>> ListSdJwtCredentialAsync(IAgentContext agentContext, ISearchQuery query = null, int count = 100, int skip = 0);
        Task StoreSdJwtCredentialAsync(IAgentContext agentContext, SdJwtCredentialRecord sdJwtCredentialRecord);
        Task<OpenId4VciRecord> GetVciRecordAsnyc(IAgentContext agentContext, string recordId);
        Task<List<OpenId4VciRecord>> ListVciRecordAsync(IAgentContext agentContext, ISearchQuery query = null, int count = 100, int skip = 0);
        Task StoreVciRecordAsync(IAgentContext agentContext, OpenId4VciRecord openId4VciRecord);

    }
}
