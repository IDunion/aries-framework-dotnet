using anoncreds_rs_dotnet.Models;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Revocation.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Revocation.Abstractions
{
    public interface IRevocationService
    {
        public Task CreateAndRegisterRevocationRegistryDefinitionAsync(IAgentContext context, string originDid, CredentialDefinition credDefObject, string credDefId, string tag, RegistryType revRegType, long maxCredNumber, string tailsDirPath);
    }
}
