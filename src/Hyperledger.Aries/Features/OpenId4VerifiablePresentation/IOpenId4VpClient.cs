using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Features.OpenId4VerifiablePresentation.Models;

namespace Hyperledger.Aries.Features.OpenId4VerifiablePresentation
{
    public interface IOpenId4VpClient
    {
        /// <summary>
        /// Processes the AuthenticationRequest url of an OpenId4VP request
        /// </summary>
        /// <param name="agentContext">The agent context.</param>
        /// <param name="url">The authentication request url.</param>
        /// <returns>The proof record wherein the request is stored.</returns>
        Task<string> ProcessAuthenticationRequestUrl(IAgentContext agentContext, string url);

        /// <summary>
        /// Generates the Authentication Response for OpenId4VP.
        /// </summary>
        /// <param name="agentContext">The agent context.</param>
        /// <param name="authRecordId">The OpenId4VPRecord.</param>
        /// <param name="credRecordId">The credential record id.</param>
        /// <returns>Either null when the response method is direct_post otherwise it will return an Authentication
        /// Response Callback URL</returns>
        Task<string> GenerateAuthenticationResponse(IAgentContext agentContext, string authRecordId, string credRecordId);
        
        public Task<OpenId4VpRecord> ProcessAuthorizationRequestAsync(IAgentContext agentContext, AuthorizationRequest authorizationRequest);
    }
}
