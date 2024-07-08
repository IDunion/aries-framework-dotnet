using anoncreds_rs_dotnet.Anoncreds;
using anoncreds_rs_dotnet.Models;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Contracts;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Anoncreds.AnoncredsSchema
{
    public class DefaultSchemaService : ISchemaService
    {
        public readonly ILedgerService LedgerService;
        public async Task CreateAndRegisterSchemaAsync(IAgentContext context, string issuerDid, string schemaName, string schemaVersion, List<string> attrNames)
        {
            var schemaJson = await SchemaApi.CreateSchemaJsonAsync(issuerDid, schemaName, schemaVersion, attrNames);

            await LedgerService.RegisterSchemaAsync(context, issuerDid, schemaJson);
        }
    }
}
