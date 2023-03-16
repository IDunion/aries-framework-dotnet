using System.Collections.Generic;
using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Features.OpenID4Common.Records;
using Hyperledger.Aries.Storage;

namespace Hyperledger.Aries.Features.OpenId4VCI
{
    public interface IOpenId4VCIService
    {
        Task<OpenId4VciRecord> ProcessCredentialOfferAsync(IAgentContext context, string offer);
        Task<OpenId4VciRecord> RequestCredentialAsync(IAgentContext context, string recordId, string keyRefId, string userPin = null);
        Task<SdJwtCredentialRecord> GetSdJwtCredentialAsnyc(IAgentContext agentContext, string recordId);
        Task<List<SdJwtCredentialRecord>> ListSdJwtCredentialAsync(IAgentContext agentContext, ISearchQuery query = null, int count = 100, int skip = 0);
        Task<OpenId4VciRecord> GetVciRecordAsnyc(IAgentContext agentContext, string recordId);
        Task<List<OpenId4VciRecord>> ListVciRecordAsync(IAgentContext agentContext, ISearchQuery query = null, int count = 100, int skip = 0);
    }
}
