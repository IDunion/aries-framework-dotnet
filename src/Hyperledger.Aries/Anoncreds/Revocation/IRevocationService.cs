using anoncreds_rs_dotnet.Models;
using Hyperledger.Aries.Agents;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Anoncreds.Revocation
{
    public interface IRevocationService
    {
        public Task CreateAndRegisterRevocationRegistryDefinitionAsync(IAgentContext context, string originDid, CredentialDefinition credDefObject, string credDefId, string tag, RegistryType revRegType, long maxCredNumber, string tailsDirPath);
    }
}
