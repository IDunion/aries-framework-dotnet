using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Models.Records;
using Hyperledger.Indy.AnonCredsApi;
using Hyperledger.Indy.PoolApi;
using Hyperledger.Indy.WalletApi;
using Newtonsoft.Json.Linq;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Payments;
using Hyperledger.Aries.Storage;
using Microsoft.Extensions.Options;
using Flurl;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Aries.Features.IssueCredential.Models;
using Hyperledger.Aries.Features.IssueCredential.Records;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Ledger.Models;

namespace Hyperledger.Aries.Features.IssueCredential
{
    /// <inheritdoc />
    public class DefaultSchemaService : ISchemaService
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
        public DefaultSchemaService(
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
            var schema = await AnonCreds.IssuerCreateSchemaAsync(issuerDid, name, version, attributeNames.ToJson());

            var schemaRecord = new SchemaRecord
            {
                Id = schema.SchemaId,
                Name = name,
                Version = version,
                AttributeNames = attributeNames
            };

            var paymentInfo = await paymentService.GetTransactionCostAsync(context, TransactionTypes.SCHEMA);
            await LedgerService.RegisterSchemaAsync(context, issuerDid, schema.SchemaJson, paymentInfo);

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
        [Obsolete("This method is obsolete. Please use 'CreateCredentialDefinitionAsync(IAgentContext, CredentialDefinitionConfiguration)'")]
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
        public async Task<string> CreateCredentialDefinitionAsync(IAgentContext agentContext, CredentialDefinitionConfiguration configuration)
        {
            if (agentContext.AriesStorage.Wallet is null)
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Indy.WalletApi.Wallet)} which must not be null.");
            }

            if (configuration == null) throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, "Configuration must be specified.");
            if (configuration.SchemaId == null) throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, "SchemaId must be specified.");
            if (configuration.EnableRevocation &&
                configuration.RevocationRegistryBaseUri == null &&
                AgentOptions.RevocationRegistryUriPath == null) throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, "RevocationRegistryBaseUri must be specified either in the configuration or the AgentOptions");

            var schema = await LedgerService.LookupSchemaAsync(agentContext, configuration.SchemaId);

            var provisioning = await ProvisioningService.GetProvisioningAsync(agentContext.AriesStorage);
            configuration.IssuerDid ??= provisioning.IssuerDid;

            var credentialDefinition = await AnonCreds.IssuerCreateAndStoreCredentialDefAsync(
                wallet: agentContext.AriesStorage.Wallet,
                issuerDid: configuration.IssuerDid,
                schemaJson: schema.ObjectJson,
                tag: configuration.Tag,
                type: null,
                configJson: new { support_revocation = configuration.EnableRevocation }.ToJson());

            var definitionRecord = new DefinitionRecord();
            definitionRecord.IssuerDid = configuration.IssuerDid;

            //var paymentInfo = await paymentService.GetTransactionCostAsync(context, TransactionTypes.CRED_DEF);

            await LedgerService.RegisterCredentialDefinitionAsync(
                context: agentContext,
                submitterDid: configuration.IssuerDid,
                data: credentialDefinition.CredDefJson,
                paymentInfo: null);

            definitionRecord.SupportsRevocation = configuration.EnableRevocation;
            definitionRecord.Id = credentialDefinition.CredDefId;
            definitionRecord.SchemaId = configuration.SchemaId;

            if (configuration.EnableRevocation)
            {
                definitionRecord.MaxCredentialCount = configuration.RevocationRegistrySize;
                definitionRecord.RevocationAutoScale = configuration.RevocationRegistryAutoScale;

                var (_, revocationRecord) = await CreateRevocationRegistryAsync(
                    agentContext: agentContext,
                    tag: $"1-{configuration.RevocationRegistrySize}",
                    definitionRecord: definitionRecord);
                definitionRecord.CurrentRevocationRegistryId = revocationRecord.Id;
            }

            await RecordService.AddAsync(agentContext.AriesStorage, definitionRecord);

            return credentialDefinition.CredDefId;
        }

        /// <inheritdoc />
        public async Task<(RevocationRegistryResult, RevocationRegistryRecord)> CreateRevocationRegistryAsync(
                    IAgentContext agentContext,
                    string tag,
                    DefinitionRecord definitionRecord)
        {
            if (agentContext.AriesStorage.Wallet is null)
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Indy.WalletApi.Wallet)} which must not be null.");
            }

            var tailsHandle = await TailsService.CreateTailsAsync();

            var revocationRegistryDefinitionJson = new
            {
                issuance_type = "ISSUANCE_BY_DEFAULT",
                max_cred_num = definitionRecord.MaxCredentialCount
            }.ToJson();
            var revocationRegistry = await AnonCreds.IssuerCreateAndStoreRevocRegAsync(
                wallet: agentContext.AriesStorage.Wallet,
                issuerDid: definitionRecord.IssuerDid,
                type: null,
                tag: tag,
                credDefId: definitionRecord.Id,
                configJson: revocationRegistryDefinitionJson,
                tailsWriter: tailsHandle);

            var revocationRecord = new RevocationRegistryRecord
            {
                Id = revocationRegistry.RevRegId,
                CredentialDefinitionId = definitionRecord.Id
            };

            // Update tails location URI
            var revocationDefinition = JObject.Parse(revocationRegistry.RevRegDefJson);
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
                context: agentContext,
                submitterDid: definitionRecord.IssuerDid,
                data: revocationDefinition.ToString(),
                paymentInfo: null);

            await RecordService.AddAsync(agentContext.AriesStorage, revocationRecord);

            await LedgerService.SendRevocationRegistryEntryAsync(
                context: agentContext,
                issuerDid: definitionRecord.IssuerDid,
                revocationRegistryDefinitionId: revocationRegistry.RevRegId,
                revocationDefinitionType: "CL_ACCUM",
                value: revocationRegistry.RevRegEntryJson,
                paymentInfo: null);

            RevocationRegistryResult revocationRegistryResult = new RevocationRegistryResult(
                revocationRegistryId: revocationRegistry.RevRegId, 
                revocationRegistryDefinitionJson: revocationRegistry.RevRegDefJson, 
                revocationRegistryDefinitionPrivateJson: null,
                revocationRegistryJson: revocationRegistry.RevRegEntryJson
                );
            
            return (revocationRegistryResult, revocationRecord);
        }

        /// <inheritdoc />
        [Obsolete("This method is obsolete. Please use 'CreateCredentialDefinitionAsync(IAgentContext, CredentialDefinitionConfiguration)'")]
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
