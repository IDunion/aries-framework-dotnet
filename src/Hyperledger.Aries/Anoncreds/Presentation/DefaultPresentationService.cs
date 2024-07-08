using System;
using anoncreds_rs_dotnet.Models;
using System.Threading.Tasks;
using System.Collections.Generic;
using anoncreds_rs_dotnet.Anoncreds;
using Hyperledger.Aries.Features.IssueCredential;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Contracts;
using Newtonsoft.Json.Linq;

namespace Hyperledger.Aries.Anoncreds.Presentation
{
    public class DefaultPresentationService : IPresentationService
    {
        ISchemaService schemaService;
        ILedgerService ledgerService;

        public async Task<bool> VerifyPresentationAsync(IAgentContext context, string presReqJson, string presentationJson)
        {
            var presentation = PresentationApi.CreatePresentationFromJsonAsync(presentationJson);
            var presentationRequest = PresentationRequestApi.CreatePresReqFromJsonAsync(presReqJson);

            List<string> schemaIds = new List<string>(), credentialDefinitionIds = new List<string>(), revocationRegistryDefinitionIds = new List<string>();
            var schemas = new List<Schema>();
            var credentialDefinitions = new List<CredentialDefinition>();
            var revocationRegistryDefinitions = new List<RevocationRegistryDefinition>();
            var revocationStatusList = new List<RevocationStatusList>();

            foreach (var identifier in presentation.Result.Identifiers)
            {
                schemaIds.Add(identifier.SchemaId.ToString());
                credentialDefinitionIds.Add(identifier.CredentialDefinitionId.ToString());
                revocationRegistryDefinitionIds.Add(identifier.RevocationRegistryId.ToString());

                var schema = await schemaService.LookupSchemaAsync(context, identifier.SchemaId.ToString());
                schemas.Add(new Schema { JsonString = schema }) ;

                var credentialDef = await schemaService.LookupCredentialDefinitionAsync(context, identifier.CredentialDefinitionId.ToString());
                credentialDefinitions.Add(new CredentialDefinition { JsonString = credentialDef });

                var revRegDefinition = await ledgerService.LookupRevocationRegistryDefinitionAsync(context, identifier.RevocationRegistryId.ToString());
                revocationRegistryDefinitions.Add(new RevocationRegistryDefinition { JsonString = revRegDefinition.ObjectJson });

                var revocationStatus = await ledgerService.LookupRevocationRegistryAsync(context, identifier.RevocationRegistryId.ToString(), identifier.Timestamp);
                revocationStatusList.Add(new RevocationStatusList { JsonString = revocationStatus.ObjectJson });
            }
            return await PresentationApi.VerifyPresentationAsync(presentation.Result, presentationRequest.Result, schemas, schemaIds, credentialDefinitions, credentialDefinitionIds, revocationRegistryDefinitions, revocationRegistryDefinitionIds, revocationStatusList);
        }
    }
}
