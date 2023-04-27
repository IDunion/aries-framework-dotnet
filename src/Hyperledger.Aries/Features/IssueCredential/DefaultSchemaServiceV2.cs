using anoncreds_rs_dotnet.Models;
using Flurl;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Features.IssueCredential.Models;
using Hyperledger.Aries.Features.IssueCredential.Records;
using Hyperledger.Aries.Ledger.Models;
using Hyperledger.Aries.Models.Records;
using Hyperledger.Aries.Payments;
using Hyperledger.Aries.Payments.Models;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Storage.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Hyperledger.Aries.Common.AnoncredsModelExtensions;
using Anoncreds = anoncreds_rs_dotnet.Anoncreds;

namespace Hyperledger.Aries.Features.IssueCredential
{
    /// <inheritdoc />
    public class DefaultSchemaServiceV2 : ISchemaService
    {
        /// <summary>The provisioning service</summary>
        // ReSharper disable InconsistentNaming
        protected readonly IProvisioningService ProvisioningService;
        /// <summary>The record service</summary>
        protected readonly IWalletRecordService RecordService;
        /// <summary>The ledger service</summary>
        protected readonly ILedgerService LedgerService;
        private readonly IPaymentService paymentService;

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
        /// <param name="agentOptions">The agent options.</param>
        public DefaultSchemaServiceV2(
            IProvisioningService provisioningService,
            IWalletRecordService recordService,
            ILedgerService ledgerService,
            IPaymentService paymentService,
            IOptions<AgentOptions> agentOptions)
        {
            ProvisioningService = provisioningService;
            RecordService = recordService;
            LedgerService = ledgerService;
            this.paymentService = paymentService;
            AgentOptions = agentOptions.Value;
        }

        /// <inheritdoc />
        public virtual async Task<string> CreateSchemaAsync(IAgentContext context, string issuerDid, string name,
            string version, string[] attributeNames)
        {
            string schemaJson = await Anoncreds.SchemaApi.CreateSchemaJsonAsync(issuerDid, name, version, attributeNames.ToList());
            string schemaId = issuerDid;
            if (!schemaId.Contains(":"))
            {
                schemaId += $":2:{name}:{version}";
            }
            SchemaRecord schemaRecord = new()
            {
                Id = schemaId,
                Name = name,
                Version = version,
                AttributeNames = attributeNames
            };

            /** Todo : fix workaround when new indy_vdr version is released. 
             * Workaround for compatibility with indy_vdr cause it uses old shared-rs Schema models, but latest anoncreds-rs are different.
            Old shared-rs model
            {
              "ver":"{}",
              "name":"{}"
              "version":"{}"
              "attrNames":[],
              "id":"{}"
            }

            New anoncreds-rs model
            {
              "name":"{}"
              "version":"{}"
              "attrNames":[],
              "issuerId":"{}"
            }
            **/
            schemaJson = schemaJson.ToSharedRsJson(AnoncredsModel.Schema);
            // END workaround

            TransactionCost paymentInfo = await paymentService.GetTransactionCostAsync(context, TransactionTypes.SCHEMA);
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
            ProvisioningRecord provisioning = await ProvisioningService.GetProvisioningAsync(context.AriesStorage);
            return provisioning?.IssuerDid == null
                ? throw new AriesFrameworkException(ErrorCode.RecordNotFound, "This wallet is not provisioned with issuer")
                : await CreateSchemaAsync(context, provisioning.IssuerDid, name, version, attributeNames);
        }

        /// <inheritdoc />
        public async Task<string> LookupSchemaFromCredentialDefinitionAsync(IAgentContext agentContext,
            string credentialDefinitionId)
        {
            string credDef = await LookupCredentialDefinitionAsync(agentContext, credentialDefinitionId);

            if (string.IsNullOrEmpty(credDef))
            {
                return null;
            }

            try
            {
                int schemaSequenceId = Convert.ToInt32(JObject.Parse(credDef)["schemaId"].ToString());
                return await LookupSchemaAsync(agentContext, schemaSequenceId);
            }
            catch (Exception)
            {
                // Do nothing.
            }

            return null;
        }

        /// TODO this should return a schema object
        /// <inheritdoc />
        public virtual async Task<string> LookupSchemaAsync(IAgentContext agentContext, int sequenceId)
        {
            string result = await LedgerService.LookupTransactionAsync(agentContext, null, sequenceId);

            if (!string.IsNullOrEmpty(result))
            {
                try
                {
                    JObject txnData = JObject.Parse(result)["result"]["data"]["txn"]["data"]["data"] as JObject;
                    string txnId = JObject.Parse(result)["result"]["data"]["txnMetadata"]["txnId"].ToString();

                    int seperator = txnId.LastIndexOf(':');

                    string ver = txnId.Substring(seperator + 1, txnId.Length - seperator - 1);

                    txnData.Add("id", txnId);
                    txnData.Add("ver", ver);
                    txnData.Add("seqNo", sequenceId);

                    return txnData.ToString();
                }
                catch (Exception)
                {
                    // Do nothing.
                }
            }

            return null;
        }

        /// TODO this should return a schema object
        /// <inheritdoc />
        public virtual async Task<string> LookupSchemaAsync(IAgentContext agentContext, string schemaId)
        {
            Ledger.Models.AriesResponse result = await LedgerService.LookupSchemaAsync(agentContext, schemaId);
            return result?.ObjectJson;
        }

        /// <inheritdoc />
        public virtual Task<List<SchemaRecord>> ListSchemasAsync(AriesStorage storage)
        {
            return RecordService.SearchAsync<SchemaRecord>(storage, null, null, 100);
        }

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
        [Obsolete("This method is obsolete. Please use 'CreateCredentialDefinitionAsync(INewAgentContext, CredentialDefinitionConfiguration)'")]
        public virtual async Task<string> CreateCredentialDefinitionAsync(IAgentContext context, string schemaId,
            string tag, bool supportsRevocation, int maxCredentialCount)
        {
            ProvisioningRecord provisioning = await ProvisioningService.GetProvisioningAsync(context.AriesStorage);

            Uri baseUri = null;
            if (provisioning.TailsBaseUri != null)
            {
                baseUri = new Uri(provisioning.TailsBaseUri);
            }

            return provisioning?.IssuerDid == null
                ? throw new AriesFrameworkException(ErrorCode.RecordNotFound,
                    "This wallet is not provisioned with issuer")
                : await CreateCredentialDefinitionAsync(
                context: context,
                schemaId: schemaId,
                issuerDid: provisioning.IssuerDid,
                tag: tag,
                supportsRevocation: supportsRevocation,
                maxCredentialCount: maxCredentialCount,
                tailsBaseUri: baseUri);
        }

        /// <inheritdoc />
        public async Task<string> CreateCredentialDefinitionAsync(IAgentContext context, CredentialDefinitionConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, "Configuration must be specified.");
            }

            if (configuration.SchemaId == null)
            {
                throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, "SchemaId must be specified.");
            }

            if (configuration.EnableRevocation &&
                configuration.RevocationRegistryBaseUri == null &&
                AgentOptions.RevocationRegistryUriPath == null)
            {
                throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, "RevocationRegistryBaseUri must be specified either in the configuration or the AgentOptions");
            }

            AriesResponse schema = await LedgerService.LookupSchemaAsync(context, configuration.SchemaId);

            ProvisioningRecord provisioning = await ProvisioningService.GetProvisioningAsync(context.AriesStorage);
            configuration.IssuerDid ??= provisioning.IssuerDid;

            /** Todo : fix workaround when new indy_vdr version is released. 
             * Workaround for compatibility with indy_vdr cause it uses old shared-rs Schema models, but latest anoncreds-rs are different.
            Old shared-rs model
            {
              "ver":"{}",
              "name":"{}"
              "version":"{}"
              "attrNames":[],
              "id":"{}"
            }

            New anoncreds-rs model
            {
              "name":"{}"
              "version":"{}"
              "attrNames":[],
              "issuerId":"{}"
            }
            **/
            var seqNo = JObject.Parse(schema.ObjectJson)["seqNo"].ToString();
            schema.ObjectJson = schema.ObjectJson.ToAnoncredsJson(AnoncredsModel.Schema);
            // END workaround

            (string credentialDefinitionJson, string credentialDefinitionPrivateJson, string credentialKeyCorrectnessProofJson) = await Anoncreds.CredentialDefinitionApi.CreateCredentialDefinitionJsonAsync(
                schemaId: configuration.SchemaId,
                schemaObjectJson: schema.ObjectJson,
                tag: configuration.Tag,
                issuerId: configuration.IssuerDid,
                signatureType: SignatureType.CL,
                supportRevocation: configuration.EnableRevocation);

            //Todo: fix workaround with new indy_vdr release
            string credDefId = configuration.IssuerDid;
            if (!credDefId.Contains(":"))
            {
                credDefId += $":3:CL:{seqNo}:{configuration.Tag}";
            }

            credentialDefinitionJson = credentialDefinitionJson.ToSharedRsJson(AnoncredsModel.CredDef, id: credDefId, seqNo: seqNo);
            // End workaround

            DefinitionRecord definitionRecord = new()
            {
                IssuerDid = configuration.IssuerDid,

                CredDefJson = credentialDefinitionJson,
                PrivateJson = credentialDefinitionPrivateJson,
                KeyCorrectnesProofJson = credentialKeyCorrectnessProofJson
            };

            await LedgerService.RegisterCredentialDefinitionAsync(
                context: context,
                submitterDid: configuration.IssuerDid,
                data: credentialDefinitionJson,
                paymentInfo: null);

            definitionRecord.SupportsRevocation = configuration.EnableRevocation;
            definitionRecord.Id = JObject.Parse(credentialDefinitionJson)["id"].ToString();
            definitionRecord.SchemaId = configuration.SchemaId;

            if (configuration.EnableRevocation)
            {
                definitionRecord.MaxCredentialCount = configuration.RevocationRegistrySize;
                definitionRecord.RevocationAutoScale = configuration.RevocationRegistryAutoScale;

                (RevocationRegistryResult _, RevocationRegistryRecord revocationRecord) = await CreateRevocationRegistryAsync(
                    context: context,
                    tag: $"1-{configuration.RevocationRegistrySize}",
                    definitionRecord: definitionRecord);
                definitionRecord.CurrentRevocationRegistryId = revocationRecord.Id;
            }

            await RecordService.AddAsync(context.AriesStorage, definitionRecord);

            return definitionRecord.Id;
        }

        /// <inheritdoc />
        public async Task<(RevocationRegistryResult, RevocationRegistryRecord)> CreateRevocationRegistryAsync(
                    IAgentContext context,
                    string tag,
                    DefinitionRecord definitionRecord)
        {
            IssuerType issuanceType = IssuerType.ISSUANCE_BY_DEFAULT;
            long maxCredNum = definitionRecord.MaxCredentialCount;

            string credentialDefinitionJson = await LookupCredentialDefinitionAsync(context, definitionRecord.Id);
            JObject credDefJObject = JObject.Parse(credentialDefinitionJson);
            try
            {
                _ = (long)credDefJObject["schemaId"];
                credDefJObject["schemaId"] = definitionRecord.SchemaId;
                credentialDefinitionJson = JsonConvert.SerializeObject(credDefJObject);
            }
            catch
            {
                //schema id is already a string
            }

            //workaround 
            credentialDefinitionJson = credentialDefinitionJson.ToAnoncredsJson(AnoncredsModel.CredDef);
            //

            (string revocationRegistryDefinitionJson,
             string revocationRegistryDefinitionPrivateJson) = await Anoncreds.RevocationApi.CreateRevocationRegistryDefinitionJsonAsync(
                 //originDid: definitionRecord.IssuerDid,
                 originDid: JObject.Parse(credentialDefinitionJson)["issuerId"].ToString(),
                 credDefJson: credentialDefinitionJson,
                 tag: tag,
                 revRegType: RegistryType.CL_ACCUM,
                 maxCredNumber: maxCredNum,
                 tailsDirPath: null);

            string revocationStatusListJson = await Anoncreds.RevocationApi.CreateRevocationStatusListJsonAsync(
                revRegDefId: JObject.Parse(revocationRegistryDefinitionJson)["credDefId"].ToString(),
                revRegDefJson: revocationRegistryDefinitionJson,
                issuerId: JObject.Parse(revocationRegistryDefinitionJson)["issuerId"].ToString(),
                timestamp: DateTimeOffset.Now.ToUnixTimeSeconds(),
                issuanceType: issuanceType);

            string revocationRegistryDefinitionId = JObject.Parse(revocationRegistryDefinitionJson)["issuerId"].ToString();

            RevocationRegistryRecord revocationRecord = new()
            {
                Id = revocationRegistryDefinitionId,
                CredentialDefinitionId = definitionRecord.Id,
                RevRegDefJson = revocationRegistryDefinitionJson,
                RevRegJson = JObject.Parse(revocationStatusListJson)["currentAccumulator"].ToString(),
                //RevRegJson = JObject.Parse(revocationStatusListJson)["currentAccumulator"].ToString(),  
                //Todo workaround : check for right format of RevRegJson somethin like '{ value : {accum: JObject.Parse(revocationStatusListJson)["currentAccumulator"].ToString()} }' ?
                RevRegDefPrivateJson = revocationRegistryDefinitionPrivateJson,
                RevStatusListJson = revocationStatusListJson,
            };

            // Update tails location URI
            JObject revocationDefinition = JObject.Parse(revocationRegistryDefinitionJson);
            string tailsfile = Path.GetFileName(revocationDefinition["value"]["tailsLocation"].ToObject<string>());
            string tailsLocation = Url.Combine(
                AgentOptions.EndpointUri,
                AgentOptions.RevocationRegistryUriPath,
                tailsfile);
            revocationDefinition["value"]["tailsLocation"] = tailsLocation;
            revocationRecord.TailsFile = tailsfile;
            revocationRecord.TailsLocation = tailsLocation;

            //Todo workaround : RevocRegDef has to be converted into Shared-Rs format -> see class Common/AnoncredsModelExtensions.cs
            //string JSON = revocationRegistryDefinitionJson.ToSharedRsJson(AnoncredsModel.RevRegDef);

            await LedgerService.RegisterRevocationRegistryDefinitionAsync(
                context: context,
                submitterDid: definitionRecord.IssuerDid,
                data: revocationDefinition.ToString(),
                //data : JSON,
                paymentInfo: null);

            await RecordService.AddAsync(context.AriesStorage, revocationRecord);

            //TODO MORGEN WEITER
            await LedgerService.SendRevocationRegistryEntryAsync(
                context: context,
                issuerDid: definitionRecord.IssuerDid,
                revocationRegistryDefinitionId: revocationRegistryDefinitionId,
                revocationDefinitionType: "CL_ACCUM",
                //value: JObject.Parse(revocationStatusListJson)["registry"].ToString(),
                value: JObject.Parse(revocationStatusListJson)["currentAccumulator"].ToString(),
                paymentInfo: null);

            return (
                new RevocationRegistryResult(
                    revocationRegistryDefinitionId,
                    revocationRegistryDefinitionJson,
                    revocationRegistryDefinitionPrivateJson,
                    JObject.Parse(revocationStatusListJson)["registry"].ToString()),
                revocationRecord
                );
        }

        /// TODO this should return a definition object
        /// <inheritdoc />
        public virtual async Task<string> LookupCredentialDefinitionAsync(IAgentContext agentContext, string definitionId)
        {
            Ledger.Models.AriesResponse result = await LedgerService.LookupDefinitionAsync(agentContext, definitionId);
            return result?.ObjectJson;
        }

        /// <inheritdoc />
        public virtual Task<List<DefinitionRecord>> ListCredentialDefinitionsAsync(AriesStorage storage)
        {
            return RecordService.SearchAsync<DefinitionRecord>(storage, null, null, 100);
        }

        /// <inheritdoc />
        public virtual Task<DefinitionRecord> GetCredentialDefinitionAsync(AriesStorage storage, string credentialDefinitionId)
        {
            return RecordService.GetAsync<DefinitionRecord>(storage, credentialDefinitionId);
        }
    }
}
