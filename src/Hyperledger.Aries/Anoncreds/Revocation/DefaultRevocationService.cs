using anoncreds_rs_dotnet.Anoncreds;
using anoncreds_rs_dotnet.Models;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Contracts;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace Hyperledger.Aries.Anoncreds.Revocation
{
    public class DefaultRevocationService : IRevocationService
    {
        public readonly ILedgerService LedgerService;
        private readonly IProvisioningService ProvisioningService;

        public Regex SupportedIdentifiersRegex => throw new NotImplementedException();

        public async Task CreateAndRegisterRevocationRegistryDefinitionAsync(IAgentContext context, string originDid, CredentialDefinition credDefObject, string credDefId, string tag, RegistryType revRegType, long maxCredNumber, string tailsDirPath)
        {
            var req = await RevocationApi.CreateRevocationRegistryDefinitionAsync(originDid, credDefObject, credDefId, tag, revRegType, maxCredNumber, tailsDirPath);
            RevocationRegistryDefinition revRegDef = req.Item1;
            RevocationRegistryDefinitionPrivate revRegDefPrivate = req.Item2;

            var provisioning = await ProvisioningService.GetProvisioningAsync(context.Wallet);
            revRegDef.IssuerId ??= provisioning.IssuerDid;

            await LedgerService.RegisterRevocationRegistryDefinitionAsync(
               context: context,
               submitterDid: revRegDef.IssuerId,
               data: revRegDef.JsonString,
               paymentInfo: null);
        }
    }
}
