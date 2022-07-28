﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Models.Records;
using Hyperledger.Indy.AnonCredsApi;
using Hyperledger.Indy.WalletApi;
using Newtonsoft.Json.Linq;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Ledger;
using Hyperledger.Aries.Payments;
using Hyperledger.Aries.Storage;
using Microsoft.Extensions.Options;
using IndySharedRsSchema = indy_shared_rs_dotnet.IndyCredx.SchemaApi;
using IndySharedRsRevoc = indy_shared_rs_dotnet.IndyCredx.RevocationApi;
using Flurl;
using aries_askar_dotnet.Models;
using System.Linq;
using indy_shared_rs_dotnet.Models;
using Hyperledger.Aries.Features.IssueCredential.Models;

namespace Hyperledger.Aries.Features.IssueCredential
{
    /// <inheritdoc />
    public class NewSchemaService : INewSchemaService
    {
        /// <summary>The provisioning service</summary>
        // ReSharper disable InconsistentNaming
        protected readonly INewProvisioningService ProvisioningService;
        /// <summary>The record service</summary>
        protected readonly INewWalletRecordService RecordService;
        /// <summary>The ledger service</summary>
        protected readonly INewLedgerService LedgerService;
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
            INewProvisioningService provisioningService,
            INewWalletRecordService recordService,
            INewLedgerService ledgerService,
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

            await RecordService.AddAsync(context.WalletStore, schemaRecord);

            if (paymentInfo != null)
            {
                await RecordService.UpdateAsync(context.WalletStore, paymentInfo.PaymentAddress);
            }

            return schemaRecord.Id;
        }

        /// <inheritdoc />
        public virtual async Task<string> CreateSchemaAsync(IAgentContext context, string name,
            string version, string[] attributeNames)
        {
            var provisioning = await ProvisioningService.GetProvisioningAsync(context.WalletStore);
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
        public virtual Task<List<SchemaRecord>> ListSchemasAsync(Store wallet) =>
            RecordService.SearchAsync<SchemaRecord>(wallet, null, null, 100);

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

            var provisioning = await ProvisioningService.GetProvisioningAsync(context.WalletStore);
            configuration.IssuerDid ??= provisioning.IssuerDid;

            var credentialDefinition = await AnonCreds.IssuerCreateAndStoreCredentialDefAsync(
                wallet: context.Wallet,
                issuerDid: configuration.IssuerDid,
                schemaJson: schema.ObjectJson,
                tag: configuration.Tag,
                type: null,
                configJson: new { support_revocation = configuration.EnableRevocation }.ToJson());

            var definitionRecord = new DefinitionRecord();
            definitionRecord.IssuerDid = configuration.IssuerDid;

            //var paymentInfo = await paymentService.GetTransactionCostAsync(context, TransactionTypes.CRED_DEF);

            await LedgerService.RegisterCredentialDefinitionAsync(
                context: context,
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
                    context: context,
                    tag: $"1-{configuration.RevocationRegistrySize}",
                    definitionRecord: definitionRecord);
                definitionRecord.CurrentRevocationRegistryId = revocationRecord.Id;
            }

            await RecordService.AddAsync(context.WalletStore, definitionRecord);

            return credentialDefinition.CredDefId;
        }

        /// <inheritdoc />
        public async Task<(RevocationRegistryResult, RevocationRegistryRecord)> CreateRevocationRegistryAsync(
                    IAgentContext context,
                    string tag,
                    DefinitionRecord definitionRecord)
        {
            //TODO : ??? - remove after fixing issue in line 276
            //var tailsHandle = await TailsService.CreateTailsAsync(); 

            IssuerType issuanceType = IssuerType.ISSUANCE_BY_DEFAULT;
            long maxCredNum = definitionRecord.MaxCredentialCount;

            string credentialDefinitionJson = await LookupCredentialDefinitionAsync(context, definitionRecord.Id);
            (string revocationRegistryDefinitionJson,
             string revocationRegistryDefinitionPrivateJson,
             string revocationRegistryJson,
             string _) = await IndySharedRsRevoc.CreateRevocationRegistryJsonAsync(
                 originDid : definitionRecord.IssuerDid,
                 credentialDefinitionJson,
                 tag : tag,
                 revRegType : RegistryType.CL_ACCUM,
                 issuanceType : issuanceType,
                 maxCredNumber : maxCredNum,
                 tailsDirPath: null // null : default path set in IndySharedRs method 
                 //TODO : ??? - investigate how to use right tailsPath, can we use infos from TailsService ? Maybe write our own NewTailsService for this?
                 //revocationRecord.TailsLocation  
                 );

            //var revocationRegistry = await AnonCreds.IssuerCreateAndStoreRevocRegAsync(
            //    wallet: context.Wallet,
            //    issuerDid: definitionRecord.IssuerDid,
            //   type: null,
            //    tag: tag,
            //    credDefId: definitionRecord.Id,
            //    configJson: revocationRegistryDefinitionJson,
            //    tailsWriter: tailsHandle);
            
            string revocationRegistryDefinitionId = await IndySharedRsRevoc.GetRevocationRegistryDefinitionAttributeAsync(revocationRegistryDefinitionJson, "id");

            var revocationRecord = new RevocationRegistryRecord
            {
                Id = revocationRegistryDefinitionId,
                CredentialDefinitionId = definitionRecord.Id
            };

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

            await RecordService.AddAsync(context.WalletStore, revocationRecord);

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
            var provisioning = await ProvisioningService.GetProvisioningAsync(context.WalletStore);
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
        public virtual Task<List<DefinitionRecord>> ListCredentialDefinitionsAsync(Store wallet) =>
            RecordService.SearchAsync<DefinitionRecord>(wallet, null, null, 100);

        /// <inheritdoc />
        public virtual Task<DefinitionRecord> GetCredentialDefinitionAsync(Store wallet, string credentialDefinitionId) =>
            RecordService.GetAsync<DefinitionRecord>(wallet, credentialDefinitionId);
    }
}
