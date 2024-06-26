using anoncreds_rs_dotnet.Models;
using Hyperledger.Aries.Agents;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Anoncreds.AnoncredsSchema
{
    public interface ISchemaService
    {
        Task CreateAndRegisterSchemaAsync(IAgentContext context, string issuerDid, string schemaName, string schemaVersion, List<string> attrNames);
    }
}
