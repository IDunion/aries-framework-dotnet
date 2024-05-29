using anoncreds_rs_dotnet.Anoncreds;
using anoncreds_rs_dotnet.Models;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Models.Records;
using Hyperledger.Aries.Revocation.Abstractions;
using Hyperledger.Aries.Revocation.Models;
using Hyperledger.Aries.Revocation.Utils;
using Hyperledger.Indy.AnonCredsApi;
using indy_vdr_dotnet.libindy_vdr;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static Hyperledger.Aries.Revocation.Models.RevRegDefinitionState;

namespace Hyperledger.Aries.Revocation
{
    public class DefaultRevocationService : IRevocationService
    {
        public readonly ILedgerService LedgerService;
        private readonly IProvisioningService ProvisioningService;
        public async Task CreateAndRegisterRevocationRegistryDefinitionAsync(IAgentContext context, string originDid, CredentialDefinition credDefObject, string credDefId, string tag, RegistryType revRegType, long maxCredNumber, string tailsDirPath)
        {
            var req = await RevocationApi.CreateRevocationRegistryDefinitionAsync(originDid, credDefObject, credDefId, tag, revRegType, maxCredNumber, tailsDirPath);
            RevocationRegistryDefinition revRegDef = req.Item1;
            RevocationRegistryDefinitionPrivate revRegDefPrivate = req.Item2;

            var provisioning = await ProvisioningService.GetProvisioningAsync(context.Wallet);
            revRegDef.IssuerId ??= provisioning.IssuerDid;

            var definitionRecord = new DefinitionRecord();
            definitionRecord.IssuerDid = revRegDef.IssuerId;

            await LedgerService.RegisterRevocationRegistryDefinitionAsync(
               context: context,
               submitterDid: revRegDef.IssuerId,
               data: revRegDef.JsonString,
               paymentInfo: null);


        }
    }
}
