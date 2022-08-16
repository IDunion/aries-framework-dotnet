using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Models.Records;
using Newtonsoft.Json.Linq;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Ledger;
using Hyperledger.Aries.Payments;
using Hyperledger.Aries.Storage;
using Microsoft.Extensions.Options;
using IndySharedRsSchema = indy_shared_rs_dotnet.IndyCredx.SchemaApi;
using IndySharedRsCredDef = indy_shared_rs_dotnet.IndyCredx.CredentialDefinitionApi;
using IndySharedRsRevoc = indy_shared_rs_dotnet.IndyCredx.RevocationApi;
using Flurl;
using aries_askar_dotnet.Models;
using System.Linq;
using indy_shared_rs_dotnet.Models;
using Hyperledger.Aries.Features.IssueCredential.Models;
using Hyperledger.Aries.Utils;
using Hyperledger.Aries.Storage.Models;

namespace Hyperledger.Aries.Features.IssueCredential
{
    /// <inheritdoc />
    public class NewSchemaService : ISchemaService
    {
        /// <summary>The provisioning service</summary>
        // ReSharper disable InconsistentNaming
        protected readonly IProvisioningService ProvisioningService;
        /// <summary>The record service</summary>
        protected readonly IWalletRecordService RecordService;
        /// <summary>The ledger service</summary>
        protected readonly ILedgerService LedgerService;
        private readonly IPaymentService paymentService;

        /// <summary>The tails service</summary>
        protected readonly ITailsService TailsService;
        /// <summary>
        /// The agent options
        /// </summary>
        protected readonly AgentOptions AgentOptions;

        // ReSharper restore InconsistentNaming

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultSchemaService" /> class.
        /// </summary>
        /// <param name="provisioningService">Provisioning service.</param>
        /// <param name="recordService">Record service.</param>
        /// <param name="ledgerService">Ledger service.</param>
        /// <param name="paymentService">The payment service.</param>
        /// <param name="tailsService">Tails service.</param>
        /// <param name="agentOptions">The agent options.</param>
        public NewSchemaService(
            IProvisioningService provisioningService,
            IWalletRecordService recordService,
            ILedgerService ledgerService,
            IPaymentService paymentService,
            ITailsService tailsService,
            IOptions<AgentOptions> agentOptions)
        {
            ProvisioningService = provisioningService;
            RecordService = recordService;
            LedgerService = ledgerService;
            this.paymentService = paymentService;
            TailsService = tailsService;
            AgentOptions = agentOptions.Value;
        }

        /// <inheritdoc />
        public virtual async Task<string> CreateSchemaAsync(IAgentContext context, string issuerDid, string name,
            string version, string[] attributeNames)
        {
            //var schema = await AnonCreds.IssuerCreateSchemaAsync(issuerDid, name, version, attributeNames.ToJson());
            uint seqNo = 0;
            string schemaJson = await IndySharedRsSchema.CreateSchemaJsonAsync(issuerDid, name, version, attributeNames.ToList(), seqNo);
            string schemaId = await IndySharedRsSchema.GetSchemaAttributeAsync(schemaJson, "id");
            var schemaRecord = new SchemaRecord
            {
                Id = schemaId,
                Name = name,
                Version = version,
                AttributeNames = attributeNames
            };

            var paymentInfo = await paymentService.GetTransactionCostAsync(context, TransactionTypes.SCHEMA);
            await LedgerService.RegisterSchemaAsync(context, issuerDid, schemaJson, paymentInfo);

            await RecordService.AddAsync(context.AriesStorage, schemaRecord);

            if (paymentInfo != null)
            {
                await RecordService.UpdateAsync(context.AriesStorage, paymentInfo.PaymentAddress);
            }

            return schemaRecord.Id;
        }

        /// <inheritdoc />
        public virtual async Task<string> CreateSchemaAsync(IAgentContext context, string name,
            string version, string[] attributeNames)
        {
            var provisioning = await ProvisioningService.GetProvisioningAsync(context.AriesStorage);
            if (provisioning?.IssuerDid == null)
            {
                throw new AriesFrameworkException(ErrorCode.RecordNotFound, "This wallet is not provisioned with issuer");
            }

            return await CreateSchemaAsync(context, provisioning.IssuerDid, name, version, attributeNames);
        }

        /// <inheritdoc />
        public async Task<string> LookupSchemaFromCredentialDefinitionAsync(IAgentContext agentContext,
            string credentialDefinitionId)
        {
            var credDef = await LookupCredentialDefinitionAsync(agentContext, credentialDefinitionId);

            if (string.IsNullOrEmpty(credDef))
                return null;

            try
            {
                var schemaSequenceId = Convert.ToInt32(JObject.Parse(credDef)["schemaId"].ToString());
                return await LookupSchemaAsync(agentContext, schemaSequenceId);
            }
            catch (Exception) { }

            return null;
        }

        /// TODO this should return a schema object
        /// <inheritdoc />
        public virtual async Task<string> LookupSchemaAsync(IAgentContext agentContext, int sequenceId)
        {
            var result = await LedgerService.LookupTransactionAsync(agentContext, null, sequenceId);

            if (!string.IsNullOrEmpty(result))
            {
                try
                {
                    var txnData = JObject.Parse(result)["result"]["data"]["txn"]["data"]["data"] as JObject;
                    var txnId = JObject.Parse(result)["result"]["data"]["txnMetadata"]["txnId"].ToString();

                    int seperator = txnId.LastIndexOf(':');

                    string ver = txnId.Substring(seperator + 1, txnId.Length - seperator - 1);

                    txnData.Add("id", txnId);
                    txnData.Add("ver", ver);
                    txnData.Add("seqNo", sequenceId);

                    return txnData.ToString();
                }
                catch (Exception) { }
            }

            return null;
        }

        /// TODO this should return a schema object
        /// <inheritdoc />
        public virtual async Task<string> LookupSchemaAsync(IAgentContext agentContext, string schemaId)
        {
            var result = await LedgerService.LookupSchemaAsync(agentContext, schemaId);
            return result?.ObjectJson;
        }

        /// <inheritdoc />
        public virtual Task<List<SchemaRecord>> ListSchemasAsync(AriesStorage storage) =>
            RecordService.SearchAsync<SchemaRecord>(storage, null, null, 100);

        /// <inheritdoc />
        [Obsolete("This method is obsolete. Please use 'CreateCredentialDefinitionAsync(INewAgentContext, CredentialDefinitionConfiguration)'")]
        public virtual Task<string> CreateCredentialDefinitionAsync(IAgentContext context, string schemaId,
            string issuerDid, string tag, bool supportsRevocation, int maxCredentialCount, Uri tailsBaseUri)
        {
            return CreateCredentialDefinitionAsync(context, new CredentialDefinitionConfiguration
            {
                SchemaId = schemaId,
                Tag = tag,
                EnableRevocation = supportsRevocation,
                RevocationRegistrySize = maxCredentialCount,
                RevocationRegistryBaseUri = tailsBaseUri.ToString(),
                RevocationRegistryAutoScale = false,
                IssuerDid = issuerDid
            });
        }

        /// <inheritdoc />
        public async Task<string> CreateCredentialDefinitionAsync(IAgentContext context, CredentialDefinitionConfiguration configuration)
        {
            if (configuration == null) throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, "Configuration must be specified.");
            if (configuration.SchemaId == null) throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, "SchemaId must be specified.");
            if (configuration.EnableRevocation &&
                configuration.RevocationRegistryBaseUri == null &&
                AgentOptions.RevocationRegistryUriPath == null) throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, "RevocationRegistryBaseUri must be specified either in the configuration or the AgentOptions");

            var schema = await LedgerService.LookupSchemaAsync(context, configuration.SchemaId);

            var provisioning = await ProvisioningService.GetProvisioningAsync(context.AriesStorage);
            configuration.IssuerDid ??= provisioning.IssuerDid;

            (string credentialDefinitionJson, string credentialDefinitionPrivateJson, string credentialKeyCorrectnessProofJson) = await IndySharedRsCredDef.CreateCredentialDefinitionJsonAsync(
                originDid: configuration.IssuerDid,
                schemaObjectJson: schema.ObjectJson,
                tag: configuration.Tag,
                indy_shared_rs_dotnet.Models.SignatureType.CL,
                supportRevocation: configuration.EnableRevocation);
            string credentialDefinitionId = await IndySharedRsCredDef.GetCredentialDefinitionAttributeAsync(credentialDefinitionJson, "id");

            var definitionRecord = new DefinitionRecord();
            definitionRecord.IssuerDid = configuration.IssuerDid;

            /** TODO: ??? - right way to add credDefJson, credDefPrivateJson and credKeyCorProofJson info? Needed for other IndySharedRs methods which also use credDefJson ***/
            definitionRecord.SetTag(TagConstants.CredDefJson, credentialDefinitionJson);
            definitionRecord.SetTag(TagConstants.CredDefPrivateJson, credentialDefinitionPrivateJson);
            definitionRecord.SetTag(TagConstants.KeyCorrectnesProofJson, credentialKeyCorrectnessProofJson);

            //var paymentInfo = await paymentService.GetTransactionCostAsync(context, TransactionTypes.CRED_DEF);

            await LedgerService.RegisterCredentialDefinitionAsync(
                context: context,
                submitterDid: configuration.IssuerDid,
                data: credentialDefinitionJson,
                paymentInfo: null);
            
            definitionRecord.SupportsRevocation = configuration.EnableRevocation;
            definitionRecord.Id = credentialDefinitionId;
            definitionRecord.SchemaId = configuration.SchemaId;

            if (configuration.EnableRevocation)
            {
                definitionRecord.MaxCredentialCount = configuration.RevocationRegistrySize;
                definitionRecord.RevocationAutoScale = configuration.RevocationRegistryAutoScale;

                var (_, revocationRecord) = await CreateRevocationRegistryAsync(
                    context: context,
                    tag: $"1-{configuration.RevocationRegistrySize}",
                    definitionRecord: definitionRecord);
                definitionRecord.CurrentRevocationRegistryId = revocationRecord.Id;
            }

            await RecordService.AddAsync(context.AriesStorage, definitionRecord);

            return credentialDefinitionId;
        }

        /// <inheritdoc />
        public async Task<(RevocationRegistryResult, RevocationRegistryRecord)> CreateRevocationRegistryAsync(
                    IAgentContext context,
                    string tag,
                    DefinitionRecord definitionRecord)
        {
            /** TODO : ??? - remove after fixing issue in line 276 **/
            //var tailsHandle = await TailsService.CreateTailsAsync(); 

            IssuerType issuanceType = IssuerType.ISSUANCE_BY_DEFAULT;
            long maxCredNum = definitionRecord.MaxCredentialCount;

            string credentialDefinitionJson = await LookupCredentialDefinitionAsync(context, definitionRecord.Id);
            (string revocationRegistryDefinitionJson,
             string revocationRegistryDefinitionPrivateJson,
             string revocationRegistryJson,
             string revocationRegistryDeltaJson) = await IndySharedRsRevoc.CreateRevocationRegistryJsonAsync(
                 originDid : definitionRecord.IssuerDid,
                 credentialDefinitionJson,
                 tag : tag,
                 revRegType : RegistryType.CL_ACCUM,
                 issuanceType : issuanceType,
                 maxCredNumber : maxCredNum,
                 tailsDirPath: null // null : default path set in IndySharedRs method 
                 /** TODO : ??? - investigate how to use right tailsPath, can we use infos from TailsService ? Maybe write our own NewTailsService for this?
                 revocationRecord.TailsLocation  **/
                 );
            
            string revocationRegistryDefinitionId = await IndySharedRsRevoc.GetRevocationRegistryDefinitionAttributeAsync(revocationRegistryDefinitionJson, "id");

            var revocationRecord = new RevocationRegistryRecord
            {
                Id = revocationRegistryDefinitionId,
                CredentialDefinitionId = definitionRecord.Id
            };
            /** TODO: ??? - right way to add revocationRegistry.. info? Needed for other IndySharedRs methods which also use revReg/DefJson ***/
            revocationRecord.SetTag(TagConstants.RevRegDefJson, revocationRegistryDefinitionJson);
            revocationRecord.SetTag(TagConstants.RevRegJson, revocationRegistryJson);
            revocationRecord.SetTag(TagConstants.RevRegDefPrivateJson, revocationRegistryDefinitionPrivateJson);
            revocationRecord.SetTag(TagConstants.RevRegDeltaJson, revocationRegistryDeltaJson);

            // Update tails location URI
            var revocationDefinition = JObject.Parse(revocationRegistryDefinitionJson);
            var tailsfile = Path.GetFileName(revocationDefinition["value"]["tailsLocation"].ToObject<string>());
            var tailsLocation = Url.Combine(
                AgentOptions.EndpointUri,
                AgentOptions.RevocationRegistryUriPath,
                tailsfile);
            revocationDefinition["value"]["tailsLocation"] = tailsLocation;
            revocationRecord.TailsFile = tailsfile;
            revocationRecord.TailsLocation = tailsLocation;

            //paymentInfo = await paymentService.GetTransactionCostAsync(context, TransactionTypes.REVOC_REG_DEF);
            await LedgerService.RegisterRevocationRegistryDefinitionAsync(
                context: context,
                submitterDid: definitionRecord.IssuerDid,
                data: revocationDefinition.ToString(),
                paymentInfo: null);

            await RecordService.AddAsync(context.AriesStorage, revocationRecord);

            await LedgerService.SendRevocationRegistryEntryAsync(
                context: context,
                issuerDid: definitionRecord.IssuerDid,
                revocationRegistryDefinitionId:revocationRegistryDefinitionId,
                revocationDefinitionType: "CL_ACCUM", //RegistryType.CL_ACCUM.ToString()
                value: revocationRegistryJson,
                paymentInfo: null);

            return (
                new RevocationRegistryResult(
                    revocationRegistryDefinitionId, 
                    revocationRegistryDefinitionJson, 
                    revocationRegistryDefinitionPrivateJson, 
                    revocationRegistryJson),
                revocationRecord
                );
        }

        /// <inheritdoc />
        [Obsolete("This method is obsolete. Please use 'CreateCredentialDefinitionAsync(INewAgentContext, CredentialDefinitionConfiguration)'")]
        public virtual async Task<string> CreateCredentialDefinitionAsync(IAgentContext context, string schemaId,
            string tag, bool supportsRevocation, int maxCredentialCount)
        {
            var provisioning = await ProvisioningService.GetProvisioningAsync(context.AriesStorage);
            if (provisioning?.IssuerDid == null)
            {
                throw new AriesFrameworkException(ErrorCode.RecordNotFound,
                    "This wallet is not provisioned with issuer");
            }

            return await CreateCredentialDefinitionAsync(
                context: context,
                schemaId: schemaId,
                issuerDid: provisioning.IssuerDid,
                tag: tag,
                supportsRevocation: supportsRevocation,
                maxCredentialCount: maxCredentialCount,
                tailsBaseUri: provisioning.TailsBaseUri != null ? new Uri(provisioning.TailsBaseUri) : null);
        }

        /// TODO this should return a definition object
        /// <inheritdoc />
        public virtual async Task<string> LookupCredentialDefinitionAsync(IAgentContext agentContext, string definitionId)
        {
            var result = await LedgerService.LookupDefinitionAsync(agentContext, definitionId);
            return result?.ObjectJson;
        }

        /// <inheritdoc />
        public virtual Task<List<DefinitionRecord>> ListCredentialDefinitionsAsync(AriesStorage storage) =>
            RecordService.SearchAsync<DefinitionRecord>(storage, null, null, 100);

        /// <inheritdoc />
        public virtual Task<DefinitionRecord> GetCredentialDefinitionAsync(AriesStorage storage, string credentialDefinitionId) =>
            RecordService.GetAsync<DefinitionRecord>(storage, credentialDefinitionId);
    }
}
