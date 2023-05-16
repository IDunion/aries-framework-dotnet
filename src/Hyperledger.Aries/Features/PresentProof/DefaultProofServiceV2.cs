using anoncreds_rs_dotnet.Models;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Decorators;
using Hyperledger.Aries.Decorators.Attachments;
using Hyperledger.Aries.Decorators.Service;
using Hyperledger.Aries.Decorators.Threading;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.Handshakes.Common;
using Hyperledger.Aries.Features.Handshakes.Connection;
using Hyperledger.Aries.Features.IssueCredential;
using Hyperledger.Aries.Features.PresentProof.Messages;
using Hyperledger.Aries.Ledger.Models;
using Hyperledger.Aries.Models.Events;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using static Hyperledger.Aries.Common.AnoncredsModelExtensions;
using Anoncreds = anoncreds_rs_dotnet.Anoncreds;

namespace Hyperledger.Aries.Features.PresentProof
{
    public class DefaultProofServiceV2 : IProofService
    {
        /// <summary>
        /// The event aggregator
        /// </summary>
        protected readonly IEventAggregator EventAggregator;

        /// <summary>
        /// The connection service
        /// </summary>
        protected readonly IConnectionService ConnectionService;

        /// <summary>
        /// The record service
        /// </summary>
        protected readonly IWalletRecordService RecordService;

        /// <summary>
        /// The provisioning service
        /// </summary>
        protected readonly IProvisioningService ProvisioningService;

        /// <summary>
        /// The ledger service
        /// </summary>
        protected readonly ILedgerService LedgerService;

        /// <summary>
        /// The logger
        /// </summary>
        protected readonly ILogger<DefaultProofServiceV2> Logger;

        /// <summary>
        /// The tails service
        /// </summary>
        protected readonly ITailsService TailsService;

        /// <summary>
        /// Message Service
        /// </summary>
        protected readonly IMessageService MessageService;

        protected readonly ISchemaService SchemaService;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultProofServiceV2"/> class.
        /// </summary>
        /// <param name="eventAggregator">The event aggregator.</param>
        /// <param name="connectionService">The connection service.</param>
        /// <param name="recordService">The record service.</param>
        /// <param name="provisioningService">The provisioning service.</param>
        /// <param name="ledgerService">The ledger service.</param>
        /// <param name="tailsService">The tails service.</param>
        /// <param name="messageService">The message service.</param>
        /// <param name="schemaService">The schema service.</param>
        /// <param name="logger">The logger.</param>
        public DefaultProofServiceV2(
            IEventAggregator eventAggregator,
            IConnectionService connectionService,
            IWalletRecordService recordService,
            IProvisioningService provisioningService,
            ILedgerService ledgerService,
            ITailsService tailsService,
            IMessageService messageService,
            ILogger<DefaultProofServiceV2> logger,
            ISchemaService schemaService)
        {
            EventAggregator = eventAggregator;
            TailsService = tailsService;
            MessageService = messageService;
            ConnectionService = connectionService;
            RecordService = recordService;
            ProvisioningService = provisioningService;
            LedgerService = ledgerService;
            Logger = logger;
            SchemaService = schemaService;
        }

        /// <inheritdoc />
        public virtual async Task<string> CreateProofAsync(IAgentContext agentContext,
            ProofRequest proofRequest, RequestedCredentials requestedCredentials)
        {
            Debug.WriteLine($"Hyperledger Aries - Calling {nameof(CreateProofAsync)}");
            var provisioningRecord = await ProvisioningService.GetProvisioningAsync(agentContext.AriesStorage);

            var credentialObjects = new List<CredentialInfo>();
            var credentialEntryJsons = new List<string>();
            var credentialProofJsons = new List<string>();
            int index = 0;

            Debug.WriteLine($"Hyperledger Aries - Going into foreach 'RequestedAttributes':");
            foreach (var attr in requestedCredentials.RequestedAttributes)
            {
                Debug.WriteLine($"Hyperledger Aries - Processing attribute: '{JsonConvert.SerializeObject(attr)}'");
                CredentialRecord credentialRecord = await RecordService.GetAsync<CredentialRecord>(agentContext.AriesStorage, attr.Value.CredentialId);
                anoncreds_rs_dotnet.Models.Credential credential = await Anoncreds.CredentialApi.CreateCredentialFromJsonAsync(credentialRecord.CredentialJson);

                Debug.WriteLine($"Hyperledger Aries - Got credential from wallet: '{JsonConvert.SerializeObject(credentialRecord)}'");
                Debug.WriteLine($"Hyperledger Aries - Recreated credential anoncreds 'CreateCredentialFromJsonAsync' method: '{JsonConvert.SerializeObject(credential)}'");

                Dictionary<string, string> attributes = new();
                credentialRecord.CredentialAttributesValues.ToList().ForEach(x => attributes.Add((string)x.Name, (string)x.Value));

                Debug.WriteLine($"Hyperledger Aries - Credential attributes are: '{JsonConvert.SerializeObject(attributes)}'");

                var credentialRevocationIdx = "";
                if (JObject.Parse(credentialRecord.CredentialJson)["signature"]["r_credential"].HasValues)
                    credentialRevocationIdx = (string)JObject.Parse(credentialRecord.CredentialJson)["signature"]["r_credential"]["i"];
                else
                    credentialRevocationIdx = null;

                credentialObjects.Add(new CredentialInfo {
                    SchemaId = credentialRecord.SchemaId,
                    CredentialDefinitionId = credentialRecord.CredentialDefinitionId,
                    RevocationRegistryId = credentialRecord.RevocationRegistryId,
                    Referent = credentialRecord.Id,
                    CredentialRevocationId = credentialRevocationIdx,
                    Attributes = attributes
                });

                Debug.WriteLine($"Hyperledger Aries - Current CredentialInfo list is: '{JsonConvert.SerializeObject(credentialObjects)}'");

                if (credentialRecord.RevocationRegistryId != null)
                {
                    Debug.WriteLine($"Hyperledger Aries - Credential supports revocation with RevocationID: '{credentialRecord.RevocationRegistryId}'");
                    uint nonRevokedTo = 0;
                    if (proofRequest.NonRevoked != null)
                        nonRevokedTo = proofRequest.NonRevoked.To;
                    else if (proofRequest.RequestedAttributes.First().Value.NonRevoked != null)
                    {
                        nonRevokedTo = proofRequest.RequestedAttributes.First().Value.NonRevoked.To;
                    }
                    Debug.WriteLine($"Hyperledger Aries - nonRevokedTo is: '{nonRevokedTo}'");

                    var registryDefinition = await LedgerService.LookupRevocationRegistryDefinitionAsync(
                        agentContext: agentContext,
                        registryId: credential.RevocationRegistryId);

                    var registryDelta = await LedgerService.LookupRevocationRegistryDeltaAsync(
                    agentContext: agentContext,
                    revocationRegistryId: credential.RevocationRegistryId,
                    // Ledger will not return correct revocation state if the 'from' field
                    // is other than 0
                    from: 0,
                    to: nonRevokedTo);

                    string revRegDefJson = registryDefinition.ObjectJson;
                    string revRegDeltaJson = registryDelta.ObjectJson;
                    Debug.WriteLine($"Hyperledger Aries - RevocationRegistryDefJson is: '{revRegDefJson}'");
                    Debug.WriteLine($"Hyperledger Aries - RevocationRegistryDeltaJson is: '{revRegDeltaJson}'");

                    //Convert 'old' revocation delta in 'new' revocationStatusList
                    string revocationStateListJson =
                        RevocationUtils.ConvertDeltaToRevocationStatusListJson(
                            revRegDefId: credential.RevocationRegistryId,
                            revRegDefJson: registryDefinition.ObjectJson.ToAnoncredsJson(AnoncredsModel.RevRegDef),
                            deltaJson: revRegDeltaJson,
                            timestamp: (long)registryDelta.Timestamp
                            );

                    Debug.WriteLine($"Hyperledger Aries before CreateOrUpdateRevocationStateAsync()");
                    Debug.WriteLine($"Hyperledger Aries - converted revocationStatusList is {revocationStateListJson}");

                    long.TryParse(credentialRevocationIdx, out var credentialRevocationId);
                    Debug.WriteLine($"Hyperledger Aries - credentialRevocationIdx is: '{credentialRevocationId}'");

                    var tailsFilePath = await TailsService.EnsureTailsExistsAsync(agentContext, credentialRecord.RevocationRegistryId);

                    string revStateJson = await Anoncreds.RevocationApi.CreateOrUpdateRevocationStateAsync(
                        revRegDefJson: revRegDefJson.ToAnoncredsJson(AnoncredsModel.RevRegDef),
                        newRevStatusListJson: revocationStateListJson,
                        //newRevStatusListJson: updatedRevocationStatusList,
                        revRegIndex: credentialRevocationId,
                        tailsPath: tailsFilePath,
                        revStateJson: null,
                        oldRevStatusListJson: null);

                    Debug.WriteLine($"Hyperledger Aries after CreateOrUpdateRevocationStateAsync() - RevocationStatusListJson: {revStateJson}");

                    credentialEntryJsons.Add(JsonConvert.SerializeObject(CredentialEntry.CreateCredentialEntryJson(credentialRecord.CredentialJson, (long)registryDelta.Timestamp, revStateJson)));
                    //credentialEntryJsons.Add(JsonConvert.SerializeObject(CredentialEntry.CreateCredentialEntryJson(credentialRecord.CredentialJson, (long)nonRevokedTo, revStateJson)));

                    Debug.WriteLine($"Hyperledger Aries current credentialEntryJsons: '{JsonConvert.SerializeObject(credentialEntryJsons)}'");
                }
                else
                {
                    credentialEntryJsons.Add(JsonConvert.SerializeObject(CredentialEntry.CreateCredentialEntry(credential)));
                    Debug.WriteLine($"Hyperledger Aries current credentialEntryJsons: '{JsonConvert.SerializeObject(credentialEntryJsons)}'");
                }
                credentialProofJsons.Add(JsonConvert.SerializeObject(new CredentialProof
                {
                    EntryIndex = index,
                    IsPredicate = Convert.ToByte(false),
                    Referent = attr.Key,
                    Reveal = Convert.ToByte(attr.Value.Revealed)
                }));
                Debug.WriteLine($"Hyperledger Aries current credentialProofJsons: '{JsonConvert.SerializeObject(credentialProofJsons)}'");
                index += 1;
            }

            Debug.WriteLine($"Hyperledger Aries - Going into foreach 'RequestedPredicates'");
            foreach (var pred in requestedCredentials.RequestedPredicates)
            {
                CredentialRecord credentialRecord = await RecordService.GetAsync<CredentialRecord>(agentContext.AriesStorage, pred.Value.CredentialId);
                anoncreds_rs_dotnet.Models.Credential credential = await Anoncreds.CredentialApi.CreateCredentialFromJsonAsync(credentialRecord.CredentialJson);

                Dictionary<string, string> attributes = new();
                credentialRecord.CredentialAttributesValues.ToList().ForEach(x => attributes.Add((string)x.Name, (string)x.Value));

                var credentialRevocationIdx = "";
                if (JObject.Parse(credentialRecord.CredentialJson)["signature"]["r_credential"].HasValues)
                    credentialRevocationIdx = (string)JObject.Parse(credentialRecord.CredentialJson)["signature"]["r_credential"]["i"];
                else
                    credentialRevocationIdx = null;

                credentialObjects.Add(new CredentialInfo 
                {
                    SchemaId = credentialRecord.SchemaId,
                    CredentialDefinitionId = credentialRecord.CredentialDefinitionId,
                    RevocationRegistryId = credentialRecord.RevocationRegistryId,
                    Referent = credentialRecord.Id,
                    CredentialRevocationId = credentialRevocationIdx,
                    Attributes = attributes
                });

                if (credentialRecord.RevocationRegistryId != null)
                {
                    uint nonRevokedTo = 0;
                    if (proofRequest.NonRevoked != null)
                        nonRevokedTo = proofRequest.NonRevoked.To;
                    else if (proofRequest.RequestedAttributes.First().Value.NonRevoked != null)
                    {
                        nonRevokedTo = proofRequest.RequestedAttributes.First().Value.NonRevoked.To;
                    }

                    var registryDefinition = await LedgerService.LookupRevocationRegistryDefinitionAsync(
                        agentContext: agentContext,
                        registryId: credential.RevocationRegistryId);
                    
                    var registryDelta = await LedgerService.LookupRevocationRegistryDeltaAsync(
                    agentContext: agentContext,
                    revocationRegistryId: credential.RevocationRegistryId,
                    // Ledger will not return correct revocation state if the 'from' field
                    // is other than 0
                    from: 0,
                    to: nonRevokedTo);

                    string revRegDefJson = registryDefinition.ObjectJson;
                    string revRegDeltaJson = registryDelta.ObjectJson;

                    //Todo
                    Debug.WriteLine($"Aries method - CreateProofAsync() - path RequestedPredicates - revocationDeltaJson: {revRegDeltaJson}");

                    //Convert 'old' revocation delta in 'new' revocationStatusList
                    string revocationStateListJson =
                        RevocationUtils.ConvertDeltaToRevocationStatusListJson(
                            revRegDefId: credential.RevocationRegistryId,
                            revRegDefJson: registryDefinition.ObjectJson.ToAnoncredsJson(AnoncredsModel.RevRegDef),
                            deltaJson: revRegDeltaJson,
                            timestamp: (long)registryDelta.Timestamp
                            );

                    //Todo
                    Debug.WriteLine($"Aries method - CreateProofAsync() - path RequestedPredicates -revocationStatusListJson: {revocationStateListJson}");

                    long.TryParse(credentialRevocationIdx, out var credentialRevocationId);
                    var tailsFilePath = await TailsService.EnsureTailsExistsAsync(agentContext, credentialRecord.RevocationRegistryId);

                    string revStateJson = await Anoncreds.RevocationApi.CreateOrUpdateRevocationStateAsync(
                        revRegDefJson: revRegDefJson.ToAnoncredsJson(AnoncredsModel.RevRegDef),
                        newRevStatusListJson: revocationStateListJson,
                        revRegIndex: credentialRevocationId,
                        tailsPath: tailsFilePath,
                        revStateJson: null,
                        oldRevStatusListJson: null);

                    //Todo
                    Debug.WriteLine($"Aries method - CreateProofAsync() - path RequestedPredicates - revocationStateJson: {revStateJson}");

                    credentialEntryJsons.Add(JsonConvert.SerializeObject(CredentialEntry.CreateCredentialEntryJson(credentialRecord.CredentialJson, (long)registryDelta.Timestamp, revStateJson)));
                }
                else
                {
                    credentialEntryJsons.Add(JsonConvert.SerializeObject(CredentialEntry.CreateCredentialEntry(credential)));
                }

                credentialProofJsons.Add(JsonConvert.SerializeObject(new CredentialProof
                {
                    EntryIndex = index,
                    IsPredicate = Convert.ToByte(true),
                    Referent = pred.Key,
                    Reveal = Convert.ToByte(pred.Value.Revealed)
                }));
                index += 1;
            }

            List<string> selfAttestNames = new();
            List<string> selfAttestValues = new();
            foreach (var pair in requestedCredentials.SelfAttestedAttributes)
            {
                selfAttestNames.Add(pair.Key);
                selfAttestValues.Add(pair.Value);
            }

            Debug.WriteLine($"Hyperledger Aries selfAttestNames: '{JsonConvert.SerializeObject(selfAttestNames)}'");
            Debug.WriteLine($"Hyperledger Aries selfAttestValues: '{JsonConvert.SerializeObject(selfAttestValues)}'");

            (var schemas, var definitions) = await BuildSchemasAndCredDefsAsync(agentContext,
                credentialObjects.Select(x => x.SchemaId).Distinct(),
                credentialObjects.Select(x => x.CredentialDefinitionId).Distinct());

            Debug.WriteLine($"Hyperledger Aries built schemas: '{JsonConvert.SerializeObject(schemas)}'");
            Debug.WriteLine($"Hyperledger Aries built credDefs: '{JsonConvert.SerializeObject(definitions)}'");

            //Not used in V2
            /**
            var revocationStates = await BuildRevocationStatesAsync(
                agentContext: agentContext,
                credentialObjects: credentialObjects,
                proofRequest: proofRequest,
                requestedCredentials: requestedCredentials);

            Debug.WriteLine($"Hyperledger Aries built revocationStates: '{revocationStates}'");
            **/

            var proofrequestJson = proofRequest.ToJson();

            var linkSecret = await LinkSecretUtils.GetLinkSecretJsonAsync(agentContext.AriesStorage, RecordService, provisioningRecord.LinkSecretId);

            Debug.WriteLine($"Hyperledger Aries - Calling CreatePresentationAsync() in Anoncreds-Rs");

            string presentation = await Anoncreds.PresentationApi.CreatePresentationAsync(
                proofrequestJson,
                credentialEntryJsons,
                credentialProofJsons,
                selfAttestNames,
                selfAttestValues,
                linkSecret,
                schemas,
                definitions);

            Debug.WriteLine($"Hyperledger Aries - CreatePresentationAsync() completed successfully");

            return presentation;
        }

        /// <inheritdoc />
        public virtual async Task<ProofRecord> CreatePresentationAsync(IAgentContext agentContext, RequestPresentationMessage requestPresentation, RequestedCredentials requestedCredentials)
        {
            var service = requestPresentation.GetDecorator<ServiceDecorator>(DecoratorNames.ServiceDecorator);

            var record = await ProcessRequestAsync(agentContext, requestPresentation, null);
            var (presentationMessage, proofRecord) = await CreatePresentationAsync(agentContext, record.Id, requestedCredentials);

            await MessageService.SendAsync(
                agentContext: agentContext,
                message: presentationMessage,
                recipientKey: service.RecipientKeys.First(),
                endpointUri: service.ServiceEndpoint,
                routingKeys: service.RoutingKeys?.ToArray());

            return proofRecord;
        }

        /// <inheritdoc />
        public virtual async Task RejectProofRequestAsync(IAgentContext agentContext, string proofRequestId)
        {
            var request = await GetAsync(agentContext, proofRequestId);

            if (request.State != ProofState.Requested)
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Proof record state was invalid. Expected '{ProofState.Requested}', found '{request.State}'");

            await request.TriggerAsync(ProofTrigger.Reject);
            await RecordService.UpdateAsync(agentContext.AriesStorage, request);
        }

        /// <inheritdoc />
        public async Task<bool> IsRevokedAsync(IAgentContext context, string credentialRecordId)
        {
            return await IsRevokedAsync(context, await RecordService.GetAsync<CredentialRecord>(context.AriesStorage, credentialRecordId));
        }

        /// <inheritdoc />
        public async Task<bool> IsRevokedAsync(IAgentContext context, CredentialRecord record)
        {
            if (record.RevocationRegistryId == null) return false;
            if (record.State == CredentialState.Offered || record.State == CredentialState.Requested) return false;
            if (record.State == CredentialState.Revoked || record.State == CredentialState.Rejected) return true;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var proofRequest = new ProofRequest
            {
                Name = "revocation check",
                Version = "1.0",
                Nonce = await Anoncreds.PresentationRequestApi.GenerateNonceAsync(),
                RequestedAttributes = new Dictionary<string, ProofAttributeInfo>
                {
                    { "referent1", new ProofAttributeInfo { Name = record.CredentialAttributesValues.First().Name } }
                },
                NonRevoked = new RevocationInterval
                {
                    From = (uint)now,
                    To = (uint)now
                }
            };

            var proof = await CreateProofAsync(context, proofRequest, new RequestedCredentials
            {
                RequestedAttributes = new Dictionary<string, RequestedAttribute>
                {
                    { "referent1", new RequestedAttribute { CredentialId = record.CredentialId, Timestamp = now, Revealed = true } }
                }
            });

            var isValid = await VerifyProofAsync(context, proofRequest.ToJson(), proof);

            if (!isValid)
            {
                await record.TriggerAsync(CredentialTrigger.Revoke);

                record.SetTag("LastRevocationCheck", now.ToString());
                await RecordService.UpdateAsync(context.AriesStorage, record);
            }

            return !isValid;
        }

        /// <inheritdoc />
        public virtual async Task<bool> VerifyProofAsync(IAgentContext agentContext, string proofRequestJson, string proofJson, bool validateEncoding = true)
        {
            Debug.WriteLine($"Hyperledger Aries - Calling {nameof(VerifyProofAsync)}");

            var proof = JsonConvert.DeserializeObject<PartialProof>(proofJson);

            // If any values are revealed, validate encoding
            // against expected values
            if (validateEncoding && proof.RequestedProof.RevealedAttributes != null)
                foreach (var attribute in proof.RequestedProof.RevealedAttributes)
                {
                    if (!CredentialUtils.CheckValidEncoding(attribute.Value.Raw, attribute.Value.Encoded))
                    {
                        throw new AriesFrameworkException(ErrorCode.InvalidProofEncoding,
                            $"The encoded value for '{attribute.Key}' is invalid. " +
                            $"Expected '{CredentialUtils.GetEncoded(attribute.Value.Raw)}'. " +
                            $"Actual '{attribute.Value.Encoded}'");
                    }
                }

            (var schemas, var definitions) = await BuildSchemasAndCredDefsAsync(agentContext,
                proof.Identifiers
                    .Select(x => x.SchemaId)
                    .Where(x => x != null)
                    .Distinct(),
                proof.Identifiers
                    .Select(x => x.CredentialDefintionId)
                    .Where(x => x != null)
                    .Distinct());

            Debug.WriteLine($"Hyperledger Aries - Built Schemas: {JsonConvert.SerializeObject(schemas)}");
            Debug.WriteLine($"Hyperledger Aries - Built CredDefs: {JsonConvert.SerializeObject(definitions)}");

            var revocationDefinitions = await BuildRevocationRegistryDefinitionsAsync(agentContext,
                proof.Identifiers
                    .Select(x => x.RevocationRegistryId)
                    .Where(x => x != null)
                    .Distinct());

            Debug.WriteLine($"Hyperledger Aries - Built RevRegDefs: {JsonConvert.SerializeObject(revocationDefinitions)}");

            //var revocationRegistries = await BuildRevocationRegistriesAsync(
            //    agentContext,
            //    proof.Identifiers.Where(x => x.RevocationRegistryId != null));

            //TODO : wait vor indy-vdr update
            //Convert 'old' revocation delta in 'new' revocationStatusList
            var revocationStateListJsons = await BuildRevocationRegistryStateListsAsync(
                agentContext,
                proof.Identifiers.Where(x => x.RevocationRegistryId != null));

            Debug.WriteLine($"Hyperledger Aries - Built RevStatusLists: {JsonConvert.SerializeObject(revocationStateListJsons)}");

            Debug.WriteLine($"Hyperledger Aries - Calling VerifyPresentationAsync() in Anoncreds-Rs");
            return await Anoncreds.PresentationApi.VerifyPresentationAsync(
                presentationJson: proofJson,
                presentationRequestJson: proofRequestJson,
                schemaJsons: schemas,
                credentialDefinitionJsons: definitions,
                //TODO : wait vor indy-vdr update, ignore revocation for now
                revocationRegistryDefinitionJsons : revocationDefinitions,
                revocationStatusListJsons : revocationStateListJsons,
                nonrevokedIntervalOverrideJsons: null
                );
        }

        /// <inheritdoc />
        public virtual async Task<bool> VerifyProofAsync(IAgentContext agentContext, string proofRecId)
        {
            var proofRecord = await GetAsync(agentContext, proofRecId);

            if (proofRecord.State != ProofState.Accepted)
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Proof record state was invalid. Expected '{ProofState.Accepted}', found '{proofRecord.State}'");

            return await VerifyProofAsync(agentContext, proofRecord.RequestJson, proofRecord.ProofJson);
        }

        /// <inheritdoc />
        public virtual Task<List<ProofRecord>> ListAsync(IAgentContext agentContext, ISearchQuery query = null,
            int count = 100) => RecordService.SearchAsync<ProofRecord>(agentContext.AriesStorage, query, null, count);

        /// <inheritdoc />
        public virtual async Task<ProofRecord> GetAsync(IAgentContext agentContext, string proofRecId)
        {
            Logger.LogInformation(LoggingEvents.GetProofRecord, "ProofRecordId {0}", proofRecId);

            return await RecordService.GetAsync<ProofRecord>(agentContext.AriesStorage, proofRecId) ??
                   throw new AriesFrameworkException(ErrorCode.RecordNotFound, "Proof record not found");
        }

        /// <inheritdoc />
        public virtual async Task<List<IssueCredential.Credential>> ListCredentialsForProofRequestAsync(IAgentContext agentContext,
            ProofRequest proofRequest, string attributeReferent)
        {
            Debug.WriteLine($"Called {nameof(ListCredentialsForProofRequestAsync)}");

            var attributeInfo = await GetProofAttributeInfo(proofRequest, attributeReferent);
            if (attributeInfo == null)
            {
                Debug.WriteLine("Hyperledger Aries - ProofAttributeInfo is null, returning emptyList of type IssueCredential.Credential");
                return new List<IssueCredential.Credential>();
            }

            Debug.WriteLine($"Hyperledger Aries - ProofAttributeInfo is: {JsonConvert.SerializeObject(attributeInfo)}");
            var credentials = await ProverSearchCredentialsForProofRequestAsync(agentContext, attributeInfo);

            return credentials.Where(x => CheckAttributes(x, attributeInfo) == true).ToList();
        }

        /// <inheritdoc />
        public async Task<PresentationAcknowledgeMessage> CreateAcknowledgeMessageAsync(IAgentContext agentContext, string proofRecordId, string status = AcknowledgementStatusConstants.Ok)
        {
            var record = await GetAsync(agentContext, proofRecordId);

            var threadId = record.GetTag(TagConstants.LastThreadId);
            var acknowledgeMessage = new PresentationAcknowledgeMessage(agentContext.UseMessageTypesHttps)
            {
                Id = threadId,
                Status = status
            };
            acknowledgeMessage.ThreadFrom(threadId);

            return acknowledgeMessage;
        }

        /// <inheritdoc />
        public virtual async Task<ProofRecord> ProcessAcknowledgeMessageAsync(IAgentContext agentContext, PresentationAcknowledgeMessage acknowledgeMessage)
        {
            var proofRecord = await this.GetByThreadIdAsync(agentContext, acknowledgeMessage.GetThreadId());

            EventAggregator.Publish(new ServiceMessageProcessingEvent
            {
                RecordId = proofRecord.Id,
                MessageType = acknowledgeMessage.Type,
                ThreadId = acknowledgeMessage.GetThreadId()
            });

            return proofRecord;
        }

        /// <inheritdoc />
        public virtual async Task<(ProposePresentationMessage, ProofRecord)> CreateProposalAsync(IAgentContext agentContext, ProofProposal proofProposal, string connectionId)
        {
            Logger.LogInformation(LoggingEvents.CreateProofRequest, "ConnectionId {0}", connectionId);

            if (proofProposal == null)
            {
                throw new ArgumentNullException(nameof(proofProposal), "You must provide a presentation preview"); ;
            }
            if (connectionId != null)
            {
                var connection = await ConnectionService.GetAsync(agentContext, connectionId);

                if (connection.State != ConnectionState.Connected)
                    throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                        $"Connection state was invalid. Expected '{ConnectionState.Connected}', found '{connection.State}'");
            }
            this.CheckProofProposalParameters(proofProposal);


            var threadId = Guid.NewGuid().ToString();
            var proofRecord = new ProofRecord
            {
                Id = Guid.NewGuid().ToString(),
                ConnectionId = connectionId,
                ProposalJson = proofProposal.ToJson(),
                State = ProofState.Proposed
            };

            proofRecord.SetTag(TagConstants.Role, TagConstants.Holder);
            proofRecord.SetTag(TagConstants.LastThreadId, threadId);

            await RecordService.AddAsync(agentContext.AriesStorage, proofRecord);

            var message = new ProposePresentationMessage(agentContext.UseMessageTypesHttps)
            {
                Id = threadId,
                Comment = proofProposal.Comment,
                PresentationPreviewMessage = new PresentationPreviewMessage(agentContext.UseMessageTypesHttps)
                {
                    ProposedAttributes = proofProposal.ProposedAttributes.ToArray(),
                    ProposedPredicates = proofProposal.ProposedPredicates.ToArray()
                },
            };
            message.ThreadFrom(threadId);
            return (message, proofRecord);
        }

        public virtual async Task<ProofRecord> ProcessProposalAsync(IAgentContext agentContext, ProposePresentationMessage proposePresentationMessage, ConnectionRecord connection)
        {
            // save in wallet

            var proofProposal = new ProofProposal
            {
                Comment = proposePresentationMessage.Comment,
                ProposedAttributes = proposePresentationMessage.PresentationPreviewMessage.ProposedAttributes.ToList<ProposedAttribute>(),
                ProposedPredicates = proposePresentationMessage.PresentationPreviewMessage.ProposedPredicates.ToList<ProposedPredicate>()
            };

            var proofRecord = new ProofRecord
            {
                Id = Guid.NewGuid().ToString(),
                ProposalJson = proofProposal.ToJson(),
                ConnectionId = connection?.Id,
                State = ProofState.Proposed
            };

            proofRecord.SetTag(TagConstants.LastThreadId, proposePresentationMessage.GetThreadId());
            proofRecord.SetTag(TagConstants.Role, TagConstants.Requestor);
            await RecordService.AddAsync(agentContext.AriesStorage, proofRecord);

            EventAggregator.Publish(new ServiceMessageProcessingEvent
            {
                RecordId = proofRecord.Id,
                MessageType = proposePresentationMessage.Type,
                ThreadId = proposePresentationMessage.GetThreadId()
            });

            return proofRecord;
        }

        /// <inheritdoc />
        public async Task<(RequestPresentationMessage, ProofRecord)> CreateRequestFromProposalAsync(IAgentContext agentContext, ProofRequestParameters requestParams,
            string proofRecordId, string connectionId)
        {
            Logger.LogInformation(LoggingEvents.CreateProofRequest, "ConnectionId {0}", connectionId);

            if (proofRecordId == null)
            {
                throw new ArgumentNullException(nameof(proofRecordId), "You must provide proof record Id");
            }
            if (connectionId != null)
            {
                var connection = await ConnectionService.GetAsync(agentContext, connectionId);

                if (connection.State != ConnectionState.Connected)
                    throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                        $"Connection state was invalid. Expected '{ConnectionState.Connected}', found '{connection.State}'");
            }

            var proofRecord = await RecordService.GetAsync<ProofRecord>(agentContext.AriesStorage, proofRecordId);
            var proofProposal = proofRecord.ProposalJson.ToObject<ProofProposal>();


            // Build Proof Request from Proposal info
            var proofRequest = new ProofRequest
            {
                Name = requestParams.Name,
                Version = requestParams.Version,
                Nonce = await Anoncreds.PresentationRequestApi.GenerateNonceAsync(),
                RequestedAttributes = new Dictionary<string, ProofAttributeInfo>(),
                NonRevoked = requestParams.NonRevoked
            };

            var attributesByReferent = new Dictionary<string, List<ProposedAttribute>>();
            foreach (var proposedAttribute in proofProposal.ProposedAttributes)
            {
                if (proposedAttribute.Referent == null)
                {
                    proposedAttribute.Referent = Guid.NewGuid().ToString();
                }

                if (attributesByReferent.TryGetValue(proposedAttribute.Referent, out var referentAttributes))
                {
                    referentAttributes.Add(proposedAttribute);
                }
                else
                {
                    attributesByReferent.Add(proposedAttribute.Referent, new List<ProposedAttribute> { proposedAttribute });
                }
            }

            foreach (var referent in attributesByReferent.AsEnumerable())
            {
                var proposedAttributes = referent.Value;
                var attributeName = proposedAttributes.Count() == 1 ? proposedAttributes.Single().Name : null;
                var attributeNames = proposedAttributes.Count() > 1 ? proposedAttributes.ConvertAll<string>(r => r.Name).ToArray() : null;


                var requestedAttribute = new ProofAttributeInfo()
                {
                    Name = attributeName,
                    Names = attributeNames,
                    Restrictions = new List<AttributeFilter>
                    {
                        new AttributeFilter {
                            CredentialDefinitionId = proposedAttributes.First().CredentialDefinitionId,
                            SchemaId = proposedAttributes.First().SchemaId,
                            IssuerDid = proposedAttributes.First().IssuerDid
                        }
                    }
                };
                proofRequest.RequestedAttributes.Add(referent.Key, requestedAttribute);
                Console.WriteLine($"Added Attribute to Proof Request \n {proofRequest.ToString()}");
            }

            foreach (var pred in proofProposal.ProposedPredicates)
            {
                if (pred.Referent == null)
                {
                    pred.Referent = Guid.NewGuid().ToString();
                }
                var predicate = new ProofPredicateInfo()
                {
                    Name = pred.Name,
                    PredicateType = pred.Predicate,
                    PredicateValue = pred.Threshold,
                    Restrictions = new List<AttributeFilter>
                    {
                        new AttributeFilter {
                            CredentialDefinitionId = pred.CredentialDefinitionId,
                            SchemaId = pred.SchemaId,
                            IssuerDid = pred.IssuerDid
                        }
                    }

                };
                proofRequest.RequestedPredicates.Add(pred.Referent, predicate);
            }

            proofRecord.RequestJson = proofRequest.ToJson();
            await proofRecord.TriggerAsync(ProofTrigger.Request);
            await RecordService.UpdateAsync(agentContext.AriesStorage, proofRecord);

            var message = new RequestPresentationMessage(agentContext.UseMessageTypesHttps)
            {
                Id = proofRecord.Id,
                Requests = new[]
                {
                    new Attachment
                    {
                        Id = "libindy-request-presentation-0",
                        MimeType = CredentialMimeTypes.ApplicationJsonMimeType,
                        Data = new AttachmentContent
                        {
                            Base64 = proofRequest
                                .ToJson()
                                .GetUTF8Bytes()
                                .ToBase64String()
                        }
                    }
                }
            };
            message.ThreadFrom(proofRecord.GetTag(TagConstants.LastThreadId));
            return (message, proofRecord);
        }

        /// <inheritdoc />
        public Task<(RequestPresentationMessage, ProofRecord)> CreateRequestAsync(
            IAgentContext agentContext,
            ProofRequest proofRequest,
            string connectionId) =>
            CreateRequestAsync(
                agentContext: agentContext,
                proofRequestJson: proofRequest?.ToJson(),
                connectionId: connectionId);

        /// <inheritdoc />
        public virtual async Task<(RequestPresentationMessage, ProofRecord)> CreateRequestAsync(IAgentContext agentContext, string proofRequestJson, string connectionId)
        {
            Logger.LogInformation(LoggingEvents.CreateProofRequest, "ConnectionId {0}", connectionId);

            if (proofRequestJson == null)
            {
                throw new ArgumentNullException(nameof(proofRequestJson), "You must provide proof request");
            }
            if (connectionId != null)
            {
                var connection = await ConnectionService.GetAsync(agentContext, connectionId);

                if (connection.State != ConnectionState.Connected)
                    throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                        $"Connection state was invalid. Expected '{ConnectionState.Connected}', found '{connection.State}'");
            }

            var threadId = Guid.NewGuid().ToString();
            var proofRecord = new ProofRecord
            {
                Id = Guid.NewGuid().ToString(),
                ConnectionId = connectionId,
                RequestJson = proofRequestJson
            };
            proofRecord.SetTag(TagConstants.Role, TagConstants.Requestor);
            proofRecord.SetTag(TagConstants.LastThreadId, threadId);
            await RecordService.AddAsync(agentContext.AriesStorage, proofRecord);

            var message = new RequestPresentationMessage(agentContext.UseMessageTypesHttps)
            {
                Id = threadId,
                Requests = new[]
                {
                    new Attachment
                    {
                        Id = "libindy-request-presentation-0",
                        MimeType = CredentialMimeTypes.ApplicationJsonMimeType,
                        Data = new AttachmentContent
                        {
                            Base64 = proofRequestJson
                                .GetUTF8Bytes()
                                .ToBase64String()
                        }
                    }
                }
            };
            message.ThreadFrom(threadId);
            return (message, proofRecord);
        }

        /// <inheritdoc />
        public virtual async Task<(RequestPresentationMessage, ProofRecord)> CreateRequestAsync(IAgentContext agentContext, ProofRequest proofRequest, bool useDidKeyFormat = false)
        {
            var (message, record) = await CreateRequestAsync(agentContext, proofRequest, null);
            var provisioning = await ProvisioningService.GetProvisioningAsync(agentContext.AriesStorage);

            message.AddDecorator(provisioning.ToServiceDecorator(useDidKeyFormat), DecoratorNames.ServiceDecorator);
            record.SetTag("RequestData", message.ToByteArray().ToBase64UrlString());

            return (message, record);
        }

        /// <inheritdoc />
        public virtual async Task<ProofRecord> ProcessRequestAsync(IAgentContext agentContext, RequestPresentationMessage requestPresentationMessage, ConnectionRecord connection)
        {
            var requestAttachment = requestPresentationMessage.Requests.FirstOrDefault(x => x.Id == "libindy-request-presentation-0")
                ?? throw new ArgumentException("Presentation request attachment not found.");

            var requestJson = requestAttachment.Data.Base64.GetBytesFromBase64().GetUTF8String();

            ProofRecord proofRecord = null;

            try
            {
                proofRecord = await this.GetByThreadIdAsync(agentContext, requestPresentationMessage.GetThreadId());
            }
            catch (AriesFrameworkException e)
            {
                if (e.ErrorCode != ErrorCode.RecordNotFound)
                {
                    throw;
                }
            }

            if (proofRecord is null)
            {
                proofRecord = new ProofRecord
                {
                    Id = Guid.NewGuid().ToString(),
                    RequestJson = requestJson,
                    ConnectionId = connection?.Id,
                    State = ProofState.Requested
                };
                proofRecord.SetTag(TagConstants.LastThreadId, requestPresentationMessage.GetThreadId());
                proofRecord.SetTag(TagConstants.Role, TagConstants.Holder);
                await RecordService.AddAsync(agentContext.AriesStorage, proofRecord);
            }
            else
            {
                await proofRecord.TriggerAsync(ProofTrigger.Request);
                proofRecord.RequestJson = requestJson;
                await RecordService.UpdateAsync(agentContext.AriesStorage, proofRecord);
            }

            EventAggregator.Publish(new ServiceMessageProcessingEvent
            {
                RecordId = proofRecord.Id,
                MessageType = requestPresentationMessage.Type,
                ThreadId = requestPresentationMessage.GetThreadId()
            });

            return proofRecord;
        }

        /// <inheritdoc />
        public virtual async Task<ProofRecord> ProcessPresentationAsync(IAgentContext agentContext, PresentationMessage presentationMessage)
        {
            var proofRecord = await this.GetByThreadIdAsync(agentContext, presentationMessage.GetThreadId());

            var requestAttachment = presentationMessage.Presentations.FirstOrDefault(x => x.Id == "libindy-presentation-0")
                ?? throw new ArgumentException("Presentation attachment not found.");

            var proofJson = requestAttachment.Data.Base64.GetBytesFromBase64().GetUTF8String();

            if (proofRecord.State != ProofState.Requested)
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Proof state was invalid. Expected '{ProofState.Requested}', found '{proofRecord.State}'");

            proofRecord.ProofJson = proofJson;
            await proofRecord.TriggerAsync(ProofTrigger.Accept);
            await RecordService.UpdateAsync(agentContext.AriesStorage, proofRecord);

            EventAggregator.Publish(new ServiceMessageProcessingEvent
            {
                RecordId = proofRecord.Id,
                MessageType = presentationMessage.Type,
                ThreadId = presentationMessage.GetThreadId()
            });

            return proofRecord;
        }

        /// <inheritdoc />
        public virtual Task<string> CreatePresentationAsync(IAgentContext agentContext, ProofRequest proofRequest, RequestedCredentials requestedCredentials) =>
            CreateProofAsync(agentContext, proofRequest, requestedCredentials);

        /// <inheritdoc />
        public virtual async Task<(PresentationMessage, ProofRecord)> CreatePresentationAsync(IAgentContext agentContext, string proofRecordId, RequestedCredentials requestedCredentials)
        {
            Debug.WriteLine($"Hyperledger Aries - Calling {nameof(CreatePresentationAsync)}");
            var record = await GetAsync(agentContext, proofRecordId);

            if (record.State != ProofState.Requested)
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Proof state was invalid. Expected '{ProofState.Requested}', found '{record.State}'");
             var proofJson = await CreatePresentationAsync(
                agentContext,
                record.RequestJson.ToObject<ProofRequest>(),
                requestedCredentials);

            record.ProofJson = proofJson;
            await record.TriggerAsync(ProofTrigger.Accept);
            await RecordService.UpdateAsync(agentContext.AriesStorage, record);

            var threadId = record.GetTag(TagConstants.LastThreadId);

            var proofMsg = new PresentationMessage(agentContext.UseMessageTypesHttps)
            {
                Id = Guid.NewGuid().ToString(),
                Presentations = new[]
                {
                    new Attachment
                    {
                        Id = "libindy-presentation-0",
                        MimeType = CredentialMimeTypes.ApplicationJsonMimeType,
                        Data = new AttachmentContent
                        {
                            Base64 = proofJson
                                .GetUTF8Bytes()
                                .ToBase64String()
                        }
                    }
                }
            };
            proofMsg.ThreadFrom(threadId);

            return (proofMsg, record);
        }

        #region Private Methods
        private async Task<(List<string>, List<string>)> BuildSchemasAndCredDefsAsync(IAgentContext agentContext, IEnumerable<string> schemaIds, IEnumerable<string> credentialDefIds)
        {
            Debug.WriteLine($"Called {nameof(BuildSchemasAndCredDefsAsync)}'");

            var schemaJsonss = new List<string>();
            var schemas = new List<Schema>();
            var credDefJsons = new List<string>();

            foreach (var schemaId in schemaIds)
            {
                var ledgerSchema = await LedgerService.LookupSchemaAsync(agentContext, schemaId);
                //workaround
                ledgerSchema.ObjectJson = ledgerSchema.ObjectJson.ToAnoncredsJson(AnoncredsModelExtensions.AnoncredsModel.Schema);
                //
                var schema = await Anoncreds.SchemaApi.CreateSchemaFromJsonAsync(ledgerSchema.ObjectJson);
                schemas.Add(schema);
                schemaJsonss.Add(schema.JsonString);
            }

            foreach (var credDefId in credentialDefIds)
            {
                var ledgerDefinition = await LedgerService.LookupDefinitionAsync(agentContext, credDefId);
                var credDefJObject = JObject.Parse(ledgerDefinition.ObjectJson);
                try
                {
                    string temp = await SchemaService.LookupSchemaFromCredentialDefinitionAsync(agentContext, credDefJObject["id"].ToString());
                    credDefJObject["schemaId"] = JObject.Parse(temp)["id"].ToString();
                    //TODO : ??? - review
                    //var seqNo = (long)credDefJObject["schemaId"];
                    //var schema = schemas.Where(x => x.SeqNo == seqNo).First();
                    //credDefJObject["schemaId"] = schema.Id;
                }
                catch
                {
                    // nothing
                }
                var credDef = await Anoncreds.CredentialDefinitionApi.CreateCredentialDefinitionFromJsonAsync(JsonConvert.SerializeObject(credDefJObject).ToAnoncredsJson(AnoncredsModelExtensions.AnoncredsModel.CredDef));
                credDefJsons.Add(credDef.JsonString);
            }

            return (schemaJsonss, credDefJsons);
        }

        private bool HasNonRevokedOnAttributeLevel(ProofRequest proofRequest)
        {
            foreach (var proofRequestRequestedAttribute in proofRequest.RequestedAttributes)
                if (proofRequestRequestedAttribute.Value.NonRevoked != null)
                    return true;

            foreach (var proofRequestRequestedPredicate in proofRequest.RequestedPredicates)
                if (proofRequestRequestedPredicate.Value.NonRevoked != null)
                    return true;

            return false;
        }

        private async Task<(AriesRegistryResponse, string)> BuildRevocationStateAsync(
            IAgentContext agentContext, CredentialInfo credentialInfo, AriesResponse registryDefinition,
            RevocationInterval nonRevoked)
        {
            Debug.WriteLine($"Called {nameof(BuildRevocationStateAsync)}'");

            var delta = await LedgerService.LookupRevocationRegistryDeltaAsync(
                agentContext: agentContext,
                revocationRegistryId: credentialInfo.RevocationRegistryId,
                // Ledger will not return correct revocation state if the 'from' field
                // is other than 0
                from: 0, //nonRevoked.From,
                to: (long)nonRevoked.To);

            //TODO : wait vor indy-vdr update
            //Convert 'old' revocation delta in 'new' revocationStatusList
            string revocationStateListJson =
                RevocationUtils.ConvertDeltaToRevocationStatusListJson(
                    revRegDefId: credentialInfo.RevocationRegistryId,
                    revRegDefJson: registryDefinition.ObjectJson.ToAnoncredsJson(AnoncredsModel.RevRegDef),
                    deltaJson: delta.ObjectJson,
                    timestamp: (long)delta.Timestamp
                    );

            var tailsFilePath = await TailsService.EnsureTailsExistsAsync(agentContext, credentialInfo.RevocationRegistryId);
            long.TryParse(credentialInfo.CredentialRevocationId, out var credentialRevocationId);

            string state = await Anoncreds.RevocationApi.CreateOrUpdateRevocationStateAsync(
                revRegDefJson: registryDefinition.ObjectJson,
                newRevStatusListJson: revocationStateListJson,
                revRegIndex: credentialRevocationId,
                tailsPath: tailsFilePath,
                revStateJson: null,
                oldRevStatusListJson: null);

            return (delta, state);
        }

        private async Task<string> BuildRevocationStatesAsync(IAgentContext agentContext,
            IEnumerable<CredentialInfo> credentialObjects,
            ProofRequest proofRequest,
            RequestedCredentials requestedCredentials)
        {
            Debug.WriteLine($"Called {nameof(BuildRevocationStatesAsync)}'");
            Debug.WriteLine($"Hyperledger Aries ProofRequest is: '{JsonConvert.SerializeObject(proofRequest)}'");

            var allCredentials = new List<RequestedAttribute>();
            allCredentials.AddRange(requestedCredentials.RequestedAttributes.Values);
            allCredentials.AddRange(requestedCredentials.RequestedPredicates.Values);

            var result = new Dictionary<string, Dictionary<string, JObject>>();

            if (proofRequest.NonRevoked == null && !HasNonRevokedOnAttributeLevel(proofRequest))
                return result.ToJson();

            Debug.WriteLine($"Hyperledger Aries going in foreach 'requestedCredential in allCredentials'");
            foreach (var requestedCredential in allCredentials)
            {
                var credential = credentialObjects.First(x => x.Referent == requestedCredential.CredentialId);
                if (credential.RevocationRegistryId == null)
                    continue;

                var registryDefinition = await LedgerService.LookupRevocationRegistryDefinitionAsync(
                    agentContext: agentContext,
                    registryId: credential.RevocationRegistryId);

                if (proofRequest.NonRevoked != null)
                {
                    (AriesRegistryResponse delta, string state) = await BuildRevocationStateAsync(
                        agentContext, credential, registryDefinition, proofRequest.NonRevoked);

                    if (!result.ContainsKey(credential.RevocationRegistryId))
                        result.Add(credential.RevocationRegistryId, new Dictionary<string, JObject>());

                    requestedCredential.Timestamp = (long)delta.Timestamp;
                    if (!result[credential.RevocationRegistryId].ContainsKey($"{delta.Timestamp}"))
                        result[credential.RevocationRegistryId].Add($"{delta.Timestamp}", JObject.Parse(state));

                    continue;
                }

                Debug.WriteLine($"Hyperledger Aries going in foreach 'proofRequestRequestedAttribute in proofRequest.RequestedAttributes'");
                foreach (var proofRequestRequestedAttribute in proofRequest.RequestedAttributes)
                {
                    var revocationInterval = proofRequestRequestedAttribute.Value.NonRevoked;
                    var (delta, state) = await BuildRevocationStateAsync(
                        agentContext, credential, registryDefinition, revocationInterval);

                    if (!result.ContainsKey(credential.RevocationRegistryId))
                        result.Add(credential.RevocationRegistryId, new Dictionary<string, JObject>());

                    requestedCredential.Timestamp = (long)delta.Timestamp;
                    if (!result[credential.RevocationRegistryId].ContainsKey($"{delta.Timestamp}"))
                        result[credential.RevocationRegistryId].Add($"{delta.Timestamp}", JObject.Parse(state));
                }

                foreach (var proofRequestRequestedPredicate in proofRequest.RequestedPredicates)
                {
                    var revocationInterval = proofRequestRequestedPredicate.Value.NonRevoked;
                    var (delta, state) = await BuildRevocationStateAsync(
                        agentContext, credential, registryDefinition, revocationInterval);

                    if (!result.ContainsKey(credential.RevocationRegistryId))
                        result.Add(credential.RevocationRegistryId, new Dictionary<string, JObject>());

                    requestedCredential.Timestamp = (long)delta.Timestamp;
                    if (!result[credential.RevocationRegistryId].ContainsKey($"{delta.Timestamp}"))
                        result[credential.RevocationRegistryId].Add($"{delta.Timestamp}", JObject.Parse(state));
                }
            }

            return result.ToJson();
        }

        private async Task<List<string>> BuildRevocationRegistriesAsync(
            IAgentContext agentContext,
            IEnumerable<ProofIdentifier> proofIdentifiers)
        {
            var result = new List<string>();
            var defEntryIndex = 0;

            foreach (var identifier in proofIdentifiers)
            {
                if (identifier.Timestamp == null) continue;

                var revocationRegistry = await LedgerService.LookupRevocationRegistryAsync(
                    agentContext,
                    identifier.RevocationRegistryId,
                    long.Parse(identifier.Timestamp));

                _ = long.TryParse(identifier.Timestamp, out var parsedTimestamp);
                result.Add(JsonConvert.SerializeObject(new RevocationRegistryEntry
                {
                    DefEntryIdx = defEntryIndex,
                    Entry = (await Anoncreds.RevocationApi.CreateRevocationRegistryFromJsonAsync(revocationRegistry.ObjectJson)).Handle,
                    Timestamp = parsedTimestamp,
                }));
                defEntryIndex++;
            }

            return result;
        }

        private async Task<List<string>> BuildRevocationRegistryStateListsAsync(
            IAgentContext agentContext,
            IEnumerable<ProofIdentifier> proofIdentifiers)
        {
            var result = new List<string>();

            foreach (var identifier in proofIdentifiers)
            {
                if (identifier.Timestamp == null) continue;

                var ledgerRegistry = await LedgerService.LookupRevocationRegistryDefinitionAsync(agentContext, identifier.RevocationRegistryId);

                var revocationRegistryDelta = await LedgerService.LookupRevocationRegistryDeltaAsync(
                    agentContext,
                    identifier.RevocationRegistryId,
                    0,
                    long.Parse(identifier.Timestamp));

                string revocationStateListJson =
                RevocationUtils.ConvertDeltaToRevocationStatusListJson(
                    revRegDefId: identifier.RevocationRegistryId,
                    revRegDefJson: ledgerRegistry.ObjectJson.ToAnoncredsJson(AnoncredsModel.RevRegDef),
                    deltaJson: revocationRegistryDelta.ObjectJson,
                    timestamp: (long)revocationRegistryDelta.Timestamp
                    );

                result.Add(revocationStateListJson);

            }

            return result;
        }

        private async Task<List<string>> BuildRevocationRegistryDefinitionsAsync(IAgentContext agentContext,
            IEnumerable<string> revocationRegistryIds)
        {
            var result = new List<string>();

            foreach (var revRegId in revocationRegistryIds)
            {
                var ledgerRegistry = await LedgerService.LookupRevocationRegistryDefinitionAsync(agentContext, revRegId);
                result.Add(ledgerRegistry.ObjectJson.ToAnoncredsJson(AnoncredsModel.RevRegDef));
            }

            return result;
        }

        private void CheckProofProposalParameters(ProofProposal proofProposal)
        {
            if (proofProposal.ProposedAttributes.Count > 1)
            {
                var attrList = proofProposal.ProposedAttributes;
                var referents = new Dictionary<string, ProposedAttribute>();

                // Check if all attributes that share referent have same requirements
                for (int i = 0; i < attrList.Count; i++)
                {
                    var attr = attrList[i];
                    if (referents.ContainsKey(attr.Referent))
                    {
                        if (referents[attr.Referent].IssuerDid != attr.IssuerDid ||
                           referents[attr.Referent].SchemaId != attr.SchemaId ||
                           referents[attr.Referent].CredentialDefinitionId != attr.CredentialDefinitionId)
                        {
                            throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, "All attributes that share a referent must have identical requirements");
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        referents.Add(attr.Referent, attr);
                    }
                }
            }

            if (proofProposal.ProposedPredicates.Count > 1)
            {
                var predList = proofProposal.ProposedPredicates;
                var referents = new Dictionary<string, ProposedPredicate>();

                for (int i = 0; i < predList.Count; i++)
                {
                    var pred = predList[i];
                    if (referents.ContainsKey(pred.Referent))
                    {
                        throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, "Proposed Predicates must all have unique referents");
                    }
                    else
                    {
                        referents.Add(pred.Referent, pred);
                    }
                }
            }
        }

        private async Task<ProofAttributeInfo> GetProofAttributeInfo(ProofRequest proofRequest, string attributeReferent)
        {
            Debug.WriteLine($"Called {nameof(GetProofAttributeInfo)}");
            ProofAttributeInfo proofAttributeInfo;

            Debug.WriteLine($"Hyperledger Aries - Searching ProofAttributeInfo for attributeReferent: {attributeReferent}");
            bool found = proofRequest.RequestedAttributes.TryGetValue(attributeReferent, out proofAttributeInfo);
            if (!found)
            {
                ProofPredicateInfo proofPredicateInfo;
                proofRequest.RequestedPredicates.TryGetValue(attributeReferent, out proofPredicateInfo);
                Debug.WriteLine($"Hyperledger Aries - Did not find ProofAttributeInfo but maybe ProofPredicateInfo? : {JsonConvert.SerializeObject(proofPredicateInfo)}");
                return proofPredicateInfo;
            }

            Debug.WriteLine($"Hyperledger Aries - Found ProofAttributeInfo: {JsonConvert.SerializeObject(proofAttributeInfo)}");
            return proofAttributeInfo;
        }

        private async Task<List<IssueCredential.Credential>> ProverSearchCredentialsForProofRequestAsync(IAgentContext agentContext,
            ProofAttributeInfo attributeInfo)
        {
            Debug.WriteLine($"Called: {nameof(ProverSearchCredentialsForProofRequestAsync)}");

            List<IssueCredential.Credential> result = new List<IssueCredential.Credential>();

            List<ISearchQuery> queryList = new List<ISearchQuery>();

            queryList.AddRange(await CheckRestrictions(attributeInfo.Restrictions));

            Debug.WriteLine($"Hyperledger Aries - Building SearchQuery to look for potential credentials");
            ISearchQuery finalQuery;
            if (queryList.Count > 1)
            {
                finalQuery = SearchQuery.Or(queryList.ToArray());
            }
            else if (queryList.Count == 1)
            {
                finalQuery = queryList[0];
            }
            else
            {
                Debug.WriteLine($"Hyperledger Aries - No SearchQuery provided returning empty List of type IssueCredential.Credential");
                return result;
            }

            Debug.WriteLine($"Hyperledger Aries - Search Credentials in wallet for query: {JsonConvert.SerializeObject(finalQuery)}");
            var credRecs = await RecordService.SearchAsync<CredentialRecord>(agentContext.AriesStorage, finalQuery, count: 2147483647);
            Debug.WriteLine($"Hyperledger Aries - Found following Credentials : {JsonConvert.SerializeObject(credRecs)}");

            foreach (var cred in credRecs)
            {
                if (!string.IsNullOrEmpty(cred.CredentialJson))
                {
                    result.Add(ConvertCredential(cred));
                    Debug.WriteLine($"Hyperledger Aries - Added Credential with Id : {cred.Id} to list of suitable Credentials for ProofRequest");
                }
            }

            return result;
        }

        private async Task<List<ISearchQuery>> CheckRestrictions(IEnumerable<AttributeFilter> restrictions)
        {
            Debug.WriteLine($"Called: {nameof(CheckRestrictions)}");
            Debug.WriteLine($"Hyperledger Aries - Provided restrictions are {JsonConvert.SerializeObject(restrictions)}");

            List<ISearchQuery> queryList = new List<ISearchQuery>();

            if (restrictions == null || !restrictions.Any())
            {
                queryList.Add(SearchQuery.Equal("State", "Issued"));
            }
            else
            {
                foreach (AttributeFilter attributeFilter in restrictions)
                {
                    List<ISearchQuery> currentQueryList = new List<ISearchQuery>();

                    currentQueryList.Add(SearchQuery.Equal("State", "Issued"));

                    if (attributeFilter.SchemaId != null)
                    {
                        currentQueryList.Add(SearchQuery.Equal("SchemaId", attributeFilter.SchemaId));
                    }
                    if (attributeFilter.SchemaIssuerDid != null)
                    {
                        currentQueryList.Add(SearchQuery.StartsWith("SchemaId", attributeFilter.SchemaIssuerDid));
                    }
                    if (attributeFilter.SchemaVersion != null)
                    {
                        currentQueryList.Add(SearchQuery.EndsWith("SchemaId", attributeFilter.SchemaVersion));
                    }
                    if (attributeFilter.SchemaName != null)
                    {
                        currentQueryList.Add(SearchQuery.Contains("SchemaId", attributeFilter.SchemaName));
                    }
                    if (attributeFilter.CredentialDefinitionId != null)
                    {
                        currentQueryList.Add(SearchQuery.Equal("CredentialDefinitionId", attributeFilter.CredentialDefinitionId));
                    }
                    if (attributeFilter.IssuerDid != null)
                    {
                        currentQueryList.Add(SearchQuery.StartsWith("CredentialDefinitionId", attributeFilter.IssuerDid));
                    }

                    queryList.Add(SearchQuery.And(currentQueryList.ToArray()));
                }
            }
            Debug.WriteLine($"Hyperledger Aries - Returning SearchQuery {JsonConvert.SerializeObject(queryList)}");
            return queryList;
        }

        private bool CheckAttributes(IssueCredential.Credential credential, ProofAttributeInfo attributeInfo)
        {
            Debug.WriteLine($"Called: {nameof(CheckAttributes)}");
            if (attributeInfo.Name != null)
            {
                Debug.WriteLine($"Check for name field to contain: {attributeInfo.Name}");
                if (credential.CredentialInfo.Attributes.ContainsKey(attributeInfo.Name))
                {
                    Debug.WriteLine($"Attribute contained in name field");
                    return true;
                }
            }
            else if (attributeInfo.Names != null && attributeInfo.Names.Any())
            {
                foreach (string name in attributeInfo.Names)
                {
                    Debug.WriteLine($"Check for names list field to contain: {name}");
                    if (!credential.CredentialInfo.Attributes.ContainsKey(name))
                    {
                        return false;
                    }
                }
                Debug.WriteLine($"All attributes contained in names list field");
                return true;
            }

            return false;
        }

        private IssueCredential.Credential ConvertCredential(CredentialRecord credentialRecord)
        {
            Debug.WriteLine($"Called: {nameof(ConvertCredential)}");
            anoncreds_rs_dotnet.Models.Credential anoncredsCredential = JsonConvert.DeserializeObject< anoncreds_rs_dotnet.Models.Credential>(credentialRecord.CredentialJson);
            IssueCredential.Credential issueCredential = new IssueCredential.Credential
            {
                CredentialInfo = new CredentialInfo
                {
                    Referent = credentialRecord.Id,
                    CredentialDefinitionId = anoncredsCredential.CredentialDefinitionId,
                    SchemaId = anoncredsCredential.SchemaId,
                    RevocationRegistryId = anoncredsCredential.RevocationRegistryId,
                    Attributes = new Dictionary<string, string>()
                }
            };

            foreach (var keyValuePair in anoncredsCredential.Values)
            {
                issueCredential.CredentialInfo.Attributes.Add(keyValuePair.Key, keyValuePair.Value.Raw);
            }

            return issueCredential;
        }
        #endregion
    }
}
