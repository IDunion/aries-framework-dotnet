using anoncreds_rs_dotnet.Models;
using Hyperledger.Aries.Agents;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Anoncreds.Presentation
{
    public interface IPresentationService
    {
       Task<bool> VerifyPresentationAsync(IAgentContext context, string presReqJson, string presentationJson);
    }
}
