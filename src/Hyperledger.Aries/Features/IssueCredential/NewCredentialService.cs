using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Decorators;
using Hyperledger.Aries.Decorators.Attachments;
using Hyperledger.Aries.Decorators.Service;
using Hyperledger.Aries.Decorators.Threading;
using Hyperledger.Aries.Decorators.PleaseAck;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.Handshakes.Common;
using Hyperledger.Aries.Features.Handshakes.Connection;
using Hyperledger.Aries.Ledger;
using Hyperledger.Aries.Models.Events;
using Hyperledger.Aries.Models.Records;
using Hyperledger.Aries.Payments;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Utils;
using Hyperledger.Indy;
using Hyperledger.Indy.AnonCredsApi;
using Hyperledger.Indy.BlobStorageApi;
using Hyperledger.Indy.DidApi;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Hyperledger.Aries.Features.IssueCredential.Models.Messages;
using Polly;
using Hyperledger.Aries.Features.RevocationNotification;
using IndySharedRsRev = indy_shared_rs_dotnet.IndyCredx.RevocationApi;
using IndySharedRsCred = indy_shared_rs_dotnet.IndyCredx.CredentialApi;
using IndySharedRsCredDef = indy_shared_rs_dotnet.IndyCredx.CredentialDefinitionApi;
using IndySharedRsCredReq = indy_shared_rs_dotnet.IndyCredx.CredentialRequestApi;
using IndySharedRsOffer = indy_shared_rs_dotnet.IndyCredx.CredentialOfferApi;
using IndySharedRsMasterSecret = indy_shared_rs_dotnet.IndyCredx.MasterSecretApi;
using IndySharedRsRevoc = indy_shared_rs_dotnet.IndyCredx.RevocationApi;
using AriesAskarStore = aries_askar_dotnet.AriesAskar.StoreApi;
using AriesAskarKey = aries_askar_dotnet.AriesAskar.KeyApi;
using AriesAskarResult = aries_askar_dotnet.AriesAskar.ResultListApi;
using indy_shared_rs_dotnet.Models;
using Hyperledger.Aries.Ledger.Models;
using aries_askar_dotnet.Models;
using Hyperledger.Aries.Features.IssueCredential.Models;

namespace Hyperledger.Aries.Features.IssueCredential
{
    /// <inheritdoc />
    public class NewCredentialService : ICredentialService
    {
        /// <summary>
        /// The event aggregator.
        /// </summary>
        protected readonly IEventAggregator EventAggregator;

        /// <summary>
        /// The ledger service
        /// </summary>
        protected readonly ILedgerService LedgerService;

        /// <summary>
        /// The connection service
        /// </summary>
        protected readonly IConnectionService ConnectionService;

        /// <summary>
        /// The record service
        /// </summary>
        protected readonly IWalletRecordService RecordService;

        /// <summary>
        /// The schema service
        /// </summary>
        protected readonly ISchemaService SchemaService;

        /// <summary>
        /// The tails service
        /// </summary>
        protected readonly ITailsService TailsService;

        /// <summary>
        /// The provisioning service
        /// </summary>
        protected readonly IProvisioningService ProvisioningService;

        /// <summary>
        /// Payment Service
        /// </summary>
        protected readonly IPaymentService PaymentService;

        /// <summary>
        /// Message Service
        /// </summary>
        protected readonly IMessageService MessageService;

        /// <summary>
        /// The logger
        /// </summary>
        protected readonly ILogger<DefaultCredentialService> Logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultCredentialService"/> class.
        /// </summary>
        /// <param name="eventAggregator">The event aggregator.</param>
        /// <param name="ledgerService">The ledger service.</param>
        /// <param name="connectionService">The connection service.</param>
        /// <param name="recordService">The record service.</param>
        /// <param name="schemaService">The schema service.</param>
        /// <param name="tailsService">The tails service.</param>
        /// <param name="provisioningService">The provisioning service.</param>
        /// <param name="paymentService">The payment service.</param>
        /// <param name="messageService">The message service</param>
        /// <param name="logger">The logger.</param>
        public NewCredentialService(
            IEventAggregator eventAggregator,
            ILedgerService ledgerService,
            IConnectionService connectionService,
            IWalletRecordService recordService,
            ISchemaService schemaService,
            ITailsService tailsService,
            IProvisioningService provisioningService,
            IPaymentService paymentService,
            IMessageService messageService,
            ILogger<DefaultCredentialService> logger)
        {
            EventAggregator = eventAggregator;
            LedgerService = ledgerService;
            ConnectionService = connectionService;
            RecordService = recordService;
            SchemaService = schemaService;
            TailsService = tailsService;
            ProvisioningService = provisioningService;
            PaymentService = paymentService;
            this.MessageService = messageService;
            Logger = logger;
        }

        /// <inheritdoc />
        public virtual async Task<CredentialRecord> GetAsync(IAgentContext agentContext, string credentialId)
        {
            var record = await RecordService.GetAsync<CredentialRecord>(agentContext.AriesStorage, credentialId);

            if (record == null)
                throw new AriesFrameworkException(ErrorCode.RecordNotFound, "Credential record not found");

            return record;
        }

        /// <inheritdoc />
        public virtual Task<List<CredentialRecord>> ListAsync(IAgentContext agentContext, ISearchQuery query = null,
            int count = 100, int skip = 0) =>
            RecordService.SearchAsync<CredentialRecord>(agentContext.AriesStorage, query, null, count, skip);

        /// <inheritdoc />
        public virtual async Task RejectOfferAsync(IAgentContext agentContext, string credentialId)
        {
            var credential = await GetAsync(agentContext, credentialId);

            if (credential.State != CredentialState.Offered)
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Credential state was invalid. Expected '{CredentialState.Offered}', found '{credential.State}'");

            await credential.TriggerAsync(CredentialTrigger.Reject);
            await RecordService.UpdateAsync(agentContext.AriesStorage, credential);
        }

        /// <inheritdoc />
        public async Task RevokeCredentialOfferAsync(IAgentContext agentContext, string offerId)
        {
            var credentialRecord = await GetAsync(agentContext, offerId);

            if (credentialRecord.State != CredentialState.Offered)
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Credential state was invalid. Expected '{CredentialState.Offered}', found '{credentialRecord.State}'");

            await RecordService.DeleteAsync<ConnectionRecord>(agentContext.AriesStorage, offerId);
        }

        /// <inheritdoc />
        public virtual async Task RejectCredentialRequestAsync(IAgentContext agentContext, string credentialId)
        {
            var credential = await GetAsync(agentContext, credentialId);

            if (credential.State != CredentialState.Requested)
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Credential state was invalid. Expected '{CredentialState.Requested}', found '{credential.State}'");

            await credential.TriggerAsync(CredentialTrigger.Reject);
            await RecordService.UpdateAsync(agentContext.AriesStorage, credential);
        }

        /// <inheritdoc />
        public virtual async Task RevokeCredentialAsync(IAgentContext agentContext, string credentialId, bool sendRevocationNotification = false)
        {
            var credentialRecord = await GetAsync(agentContext, credentialId);

            if (credentialRecord.State != CredentialState.Issued)
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Credential state was invalid. Expected '{CredentialState.Issued}', found '{credentialRecord.State}'");

            var provisioning = await ProvisioningService.GetProvisioningAsync(agentContext.AriesStorage);

            // Check if the state machine is valid for revocation
            await credentialRecord.TriggerAsync(CredentialTrigger.Revoke);

            var revocationRecord =
                await RecordService.GetAsync<RevocationRegistryRecord>(agentContext.AriesStorage,
                    credentialRecord.RevocationRegistryId);

            // Revoke the credential
            /** TODO : ??? - Format of credRevIdx like revRegistryIndex from revocationRegistryId? **/
            if(long.TryParse(credentialRecord.CredentialRevocationId.Split(':').LastOrDefault()?.Split('-').FirstOrDefault(), out long credRevIdx))
            //if (false == long.TryParse(credentialRecord.CredentialRevocationId, out long credRevIdx))
                throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, $"Invalid credentialRevocationId, has to be of type long : {credentialRecord.CredentialRevocationId}.");
            
            (string revRegUpdatedJson, string revocRegistryDeltaJson) = await IndySharedRsRev.RevokeCredentialAsync(
                revRegDefJson : revocationRecord.GetTag(TagConstants.RevRegDefJson),
                revRegJson : revocationRecord.GetTag(TagConstants.RevRegJson),
                credRevIdx : credRevIdx,
                tailsPath : revocationRecord.TailsFile);

            //TODO : ??? - check with team -> Need to update revocation Registry in wallet (see indy-sdk code indy_issuer_revoke_credential)
            revocationRecord.SetTag(TagConstants.RevRegJson, revRegUpdatedJson);

            var paymentInfo =
                await PaymentService.GetTransactionCostAsync(agentContext, TransactionTypes.REVOC_REG_ENTRY);

            // Write the delta state on the ledger for the corresponding revocation registry
            await LedgerService.SendRevocationRegistryEntryAsync(
                context: agentContext,
                issuerDid: provisioning.IssuerDid,
                revocationRegistryDefinitionId: revocationRecord.Id,
                revocationDefinitionType: "CL_ACCUM",
                value: revocRegistryDeltaJson,
                paymentInfo: paymentInfo);

            if (paymentInfo != null)
                await RecordService.UpdateAsync(agentContext.AriesStorage, paymentInfo.PaymentAddress);

            // Update local credential record
            await RecordService.UpdateAsync(agentContext.AriesStorage, credentialRecord);
            // Update local revocation record
            await RecordService.UpdateAsync(agentContext.AriesStorage, revocationRecord);

            if (!sendRevocationNotification)
                return;

            var connection = await ConnectionService.GetAsync(agentContext, credentialRecord.ConnectionId);

            Logger.LogInformation($"Sending Revocation Notification for credential {credentialId} to {connection.Endpoint}...");

            var revocationNotificationMessage = new RevocationNotificationMessage
            {
                ThreadId = credentialRecord.GetTag(TagConstants.LastThreadId)
            };
            revocationNotificationMessage.AddDecorator(new PleaseAckDecorator(new[] { OnValues.OUTCOME }), DecoratorNames.PleaseAckDecorator);

            await MessageService.SendAsync(
                agentContext,
                revocationNotificationMessage,
                connection);
        }

        /// <inheritdoc />
        public async Task DeleteCredentialAsync(IAgentContext agentContext, string credentialId)
        {
            // TODO : ??? - ProverDeleteCredentialAsync has same functionality than RecordService.DeleteAsync - delete after discussed with team
            // see indy-sdk : NonSecrets.indy_delete_wallet_record and AnonCreds.indy_prover_delete_credential lead to delete_record(wallet,type,name)
            //var credentialRecord = await GetAsync(agentContext, credentialId);
            //try
            //{
            //    /** TODO : ??? - No such function in SharedRS **/
            //    await AnonCreds.ProverDeleteCredentialAsync(agentContext.AriesStorage.Store, credentialRecord.CredentialId);
            //}
            //catch
            //{
            //    // OK
            //}

            await RecordService.DeleteAsync<CredentialRecord>(agentContext.AriesStorage, credentialId);
        }

        /// <inheritdoc />
        public async Task<CredentialAcknowledgeMessage> CreateAcknowledgementMessageAsync(IAgentContext agentContext, string credentialRecordId,
            string status = AcknowledgementStatusConstants.Ok)
        {
            var record = await GetAsync(agentContext, credentialRecordId);

            var threadId = record.GetTag(TagConstants.LastThreadId);
            var acknowledgeMessage = new CredentialAcknowledgeMessage(agentContext.UseMessageTypesHttps)
            {
                Id = threadId,
                Status = status
            };
            acknowledgeMessage.ThreadFrom(threadId);

            return acknowledgeMessage;
        }

        /// <inheritdoc />
        public async Task<CredentialRecord> ProcessAcknowledgementMessageAsync(IAgentContext agentContext,
            CredentialAcknowledgeMessage credentialAcknowledgeMessage)
        {
            var credentialRecord = await this.GetByThreadIdAsync(agentContext, credentialAcknowledgeMessage.GetThreadId());

            EventAggregator.Publish(new ServiceMessageProcessingEvent
            {
                RecordId = credentialRecord.Id,
                MessageType = credentialAcknowledgeMessage.Type,
                ThreadId = credentialAcknowledgeMessage.GetThreadId()
            });

            return credentialRecord;
        }

        /// <inheritdoc />
        public virtual async Task<string> ProcessOfferAsync(IAgentContext agentContext, CredentialOfferMessage credentialOffer,
            ConnectionRecord connection)
        {
            var offerAttachment = credentialOffer.Offers.FirstOrDefault(x => x.Id == "libindy-cred-offer-0")
                                  ?? throw new ArgumentNullException(nameof(CredentialOfferMessage.Offers));

            var offerJson = offerAttachment.Data.Base64.GetBytesFromBase64().GetUTF8String();
            var offer = JObject.Parse(offerJson);
            var definitionId = offer["cred_def_id"].ToObject<string>();
            var schemaId = offer["schema_id"].ToObject<string>();

            var threadId = credentialOffer.GetThreadId() ?? Guid.NewGuid().ToString();
            // Write offer record to local wallet
            var credentialRecord = new CredentialRecord
            {
                Id = threadId,
                OfferJson = offerJson,
                ConnectionId = connection?.Id,
                CredentialDefinitionId = definitionId,
                CredentialAttributesValues = credentialOffer.CredentialPreview?.Attributes
                    .Select(x => new CredentialPreviewAttribute
                    {
                        Name = x.Name,
                        MimeType = x.MimeType,
                        Value = x.Value
                    }).ToArray(),
                SchemaId = schemaId,
                State = CredentialState.Offered
            };
            credentialRecord.SetTag(TagConstants.Role, TagConstants.Holder);
            credentialRecord.SetTag(TagConstants.LastThreadId, threadId);

            await RecordService.AddAsync(agentContext.AriesStorage, credentialRecord);

            EventAggregator.Publish(new ServiceMessageProcessingEvent
            {
                RecordId = credentialRecord.Id,
                MessageType = credentialOffer.Type,
                ThreadId = threadId
            });

            return credentialRecord.Id;
        }

        /// <inheritdoc />
        public async Task<CredentialRecord> CreateCredentialAsync(IAgentContext agentContext,
            CredentialOfferMessage message)
        {
            var credentialRecordId = "";
            try
            {
                var service = message.GetDecorator<ServiceDecorator>(DecoratorNames.ServiceDecorator);

                credentialRecordId = await ProcessOfferAsync(agentContext, message, null);

                var (request, record) = await CreateRequestAsync(agentContext, credentialRecordId);
                var provisioning = await ProvisioningService.GetProvisioningAsync(agentContext.AriesStorage);

                try
                {
                    var credentialIssueMessage = await MessageService.SendReceiveAsync<CredentialIssueMessage>(
                        agentContext: agentContext,
                        message: request,
                        recipientKey: service.RecipientKeys.First(),
                        endpointUri: service.ServiceEndpoint,
                        routingKeys: service.RoutingKeys.ToArray(),
                        senderKey: provisioning.IssuerVerkey);
                    var recordId = await ProcessCredentialAsync(agentContext, credentialIssueMessage, null);
                    return await RecordService.GetAsync<CredentialRecord>(agentContext.AriesStorage, recordId);
                }
                catch (AriesFrameworkException ex) when (ex.ErrorCode == ErrorCode.A2AMessageTransmissionError)
                {
                    throw new AriesFrameworkException(ex.ErrorCode, ex.Message, record, null);
                }
            }
            catch (IndyException e) when (e.SdkErrorCode == 309)
            {
                throw new AriesFrameworkException(ErrorCode.LedgerItemNotFound, e.Message, credentialRecordId);
            }
        }

        /// <inheritdoc />
        public async Task<(CredentialRequestMessage, CredentialRecord)> CreateRequestAsync(IAgentContext agentContext,
            string credentialId)
        {
            var credential = await GetAsync(agentContext, credentialId);
            if (credential.State != CredentialState.Offered)
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Credential state was invalid. Expected '{CredentialState.Offered}', found '{credential.State}'");

            string proverDid = null;
            if (credential.ConnectionId != null)
            {
                var connection = await ConnectionService.GetAsync(agentContext, credential.ConnectionId);
                proverDid = connection.MyDid;
            }

            else
            {
                (string newDid, _ ) = await DidUtils.CreateAndStoreMyDidAsync(agentContext.AriesStorage.Store, RecordService);
                proverDid = newDid;
            }

            var definition = await LedgerService.LookupDefinitionAsync(agentContext, credential.CredentialDefinitionId);
            var provisioning = await ProvisioningService.GetProvisioningAsync(agentContext.AriesStorage);

            (string CredentialRequestJson, string CredentialRequestMetadataJson) = await IndySharedRsCredReq.CreateCredentialRequestJsonAsync(
                proverDid: proverDid,
                credentialDefinitionJson: definition.ObjectJson,
                masterSecretJson : await MasterSecretUtils.GetMasterSecretJsonAsync(agentContext.AriesStorage, RecordService, provisioning.MasterSecretId),
                masterSecretId: provisioning.MasterSecretId,
                credentialOfferJson: credential.OfferJson

                );

            // Update local credential record with new info
            credential.CredentialRequestMetadataJson = CredentialRequestMetadataJson;
            await credential.TriggerAsync(CredentialTrigger.Request);
            await RecordService.UpdateAsync(agentContext.AriesStorage, credential);
            var threadId = credential.GetTag(TagConstants.LastThreadId);

            var response = new CredentialRequestMessage(agentContext.UseMessageTypesHttps)
            {
                // The comment was required by Aca-py, even though it is declared optional in RFC-0036
                // Was added for interoperability
                Comment = "",
                Requests = new[]
                {
                    new Attachment
                    {
                        Id = "libindy-cred-request-0",
                        MimeType = CredentialMimeTypes.ApplicationJsonMimeType,
                        Data = new AttachmentContent
                        {
                            Base64 = CredentialRequestJson.GetUTF8Bytes().ToBase64String()
                        }
                    }
                }
            };

            response.ThreadFrom(threadId);
            return (response, credential);
        }

        /// <inheritdoc />
        public virtual async Task<string> ProcessCredentialAsync(IAgentContext agentContext, CredentialIssueMessage credential,
            ConnectionRecord connection)
        {
            var credentialAttachment = credential.Credentials.FirstOrDefault(x => x.Id == "libindy-cred-0")
                                       ?? throw new ArgumentException("Credential attachment not found");

            var credentialJson = credentialAttachment.Data.Base64.GetBytesFromBase64().GetUTF8String();
            var credentialJobj = JObject.Parse(credentialJson);
            var definitionId = credentialJobj["cred_def_id"].ToObject<string>();
            var revRegId = credentialJobj["rev_reg_id"]?.ToObject<string>();

            var credentialRecord = await Policy.Handle<AriesFrameworkException>()
                .RetryAsync(3, async (ex, retry) => { await Task.Delay((int)Math.Pow(retry, 2) * 100); })
                .ExecuteAsync(() => this.GetByThreadIdAsync(agentContext, credential.GetThreadId()));

            if (credentialRecord.State != CredentialState.Requested)
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Credential state was invalid. Expected '{CredentialState.Requested}', found '{credentialRecord.State}'");
            var credentialDefinition = await LedgerService.LookupDefinitionAsync(agentContext, definitionId);

            string revocationRegistryDefinitionJson = null;
            if (!string.IsNullOrEmpty(revRegId))
            {
                // If credential supports revocation, lookup registry definition
                var revocationRegistry =
                    await LedgerService.LookupRevocationRegistryDefinitionAsync(agentContext, revRegId);
                revocationRegistryDefinitionJson = revocationRegistry.ObjectJson;
                credentialRecord.RevocationRegistryId = revRegId;
            }

            var provisioning = await ProvisioningService.GetProvisioningAsync(agentContext.AriesStorage);

            string credentialProcessedJson = await IndySharedRsCred.ProcessCredentialAsync(
                credentialJson,
                credentialRecord.CredentialRequestMetadataJson,
                await MasterSecretUtils.GetMasterSecretJsonAsync(agentContext.AriesStorage, RecordService, provisioning.MasterSecretId),
                credentialDefinition.ObjectJson,
                revocationRegistryDefinitionJson
                );
            string credentialProcessedId = await IndySharedRsCred.GetCredentialAttributeAsync(credentialProcessedJson, "cred_def_id");

            /** TODO : ??? - need to update credentialJson information
             * we also need to check if attributes in credentialRecord need an update -> compare with indy-sdk : indy_prover_store_credential **/
            credentialRecord.SetTag(TagConstants.CredJson, credentialProcessedJson);

            credentialRecord.CredentialId = credentialProcessedId;
            await credentialRecord.TriggerAsync(CredentialTrigger.Issue);
            await RecordService.UpdateAsync(agentContext.AriesStorage, credentialRecord);
            EventAggregator.Publish(new ServiceMessageProcessingEvent
            {
                RecordId = credentialRecord.Id,
                MessageType = credential.Type,
                ThreadId = credential.GetThreadId()
            });
            return credentialRecord.Id;
        }

        /// <inheritdoc />
        public async Task<(CredentialOfferMessage, CredentialRecord)> CreateOfferAsync(
            IAgentContext agentContext, OfferConfiguration config, string connectionId)
        {
            Logger.LogInformation(LoggingEvents.CreateCredentialOffer, "DefinitionId {0}, IssuerDid {1}",
                config.CredentialDefinitionId, config.IssuerDid);

            var threadId = Guid.NewGuid().ToString();
            if (!string.IsNullOrEmpty(connectionId))
            {
                var connection = await ConnectionService.GetAsync(agentContext, connectionId);

                if (connection.State != ConnectionState.Connected)
                {
                    throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                        $"Connection state was invalid. Expected '{ConnectionState.Connected}', found '{connection.State}'");
                }
            }

            if (config.CredentialAttributeValues != null && config.CredentialAttributeValues.Any())
            {
                CredentialUtils.ValidateCredentialPreviewAttributes(config.CredentialAttributeValues);
            }

            DefinitionRecord definition = await SchemaService.GetCredentialDefinitionAsync(agentContext.AriesStorage, config.CredentialDefinitionId);
            string credDefJson = definition.GetTag(TagConstants.CredDefJson);
            string offerJson = await IndySharedRsOffer.CreateCredentialOfferJsonAsync(
                await IndySharedRsCredDef.GetCredentialDefinitionAttributeAsync(credDefJson, "schema_id"),
                credDefJson,
                definition.GetTag(TagConstants.KeyCorrectnesProofJson));

            var offerJobj = JObject.Parse(offerJson);
            var schemaId = offerJobj["schema_id"].ToObject<string>();

            // Write offer record to local wallet
            var credentialRecord = new CredentialRecord
            {
                Id = threadId,
                CredentialDefinitionId = config.CredentialDefinitionId,
                OfferJson = offerJson,
                ConnectionId = connectionId,
                SchemaId = schemaId,
                CredentialAttributesValues = config.CredentialAttributeValues,
                State = CredentialState.Offered,
            };

            credentialRecord.SetTag(TagConstants.LastThreadId, threadId);
            credentialRecord.SetTag(TagConstants.Role, TagConstants.Issuer);
            if (!string.IsNullOrEmpty(config.IssuerDid))
                credentialRecord.SetTag(TagConstants.IssuerDid, config.IssuerDid);
            if (config.Tags != null)
                foreach (var tag in config.Tags)
                {
                    if (!credentialRecord.Tags.Keys.Contains(tag.Key))
                        credentialRecord.Tags.Add(tag.Key, tag.Value);
                }

            await RecordService.AddAsync(agentContext.AriesStorage, credentialRecord);
            return (new CredentialOfferMessage(agentContext.UseMessageTypesHttps)
            {
                Id = threadId,
                Offers = new Attachment[]
                {
                    new Attachment
                    {
                        Id = "libindy-cred-offer-0",
                        MimeType = CredentialMimeTypes.ApplicationJsonMimeType,
                        Data = new AttachmentContent
                        {
                            Base64 = offerJson.GetUTF8Bytes().ToBase64String()
                        }
                    }
                },
                CredentialPreview = credentialRecord.CredentialAttributesValues != null
                    ? new CredentialPreviewMessage(agentContext.UseMessageTypesHttps)
                    {
                        Attributes = credentialRecord.CredentialAttributesValues.Select(x =>
                            new CredentialPreviewAttribute
                            {
                                Name = x.Name,
                                MimeType = x.MimeType,
                                Value = x.Value?.ToString()
                            }).ToArray()
                    }
                    : null
            }, credentialRecord);
        }

        /// <inheritdoc />
        public async Task<(CredentialOfferMessage, CredentialRecord)> CreateOfferAsync(
            IAgentContext agentContext, OfferConfiguration config)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (config.CredentialAttributeValues == null || !config.CredentialAttributeValues.Any())
            {
                throw new InvalidOperationException(
                    "You must supply credential values when creating connectionless credential offer");
            }

            var (message, record) = await CreateOfferAsync(agentContext, config, null);
            var provisioning = await ProvisioningService.GetProvisioningAsync(agentContext.AriesStorage);
            message.AddDecorator(provisioning.ToServiceDecorator(config.UseDidKeyFormat), DecoratorNames.ServiceDecorator);

            await RecordService.UpdateAsync(agentContext.AriesStorage, record);
            return (message, record);
        }

        /// <inheritdoc />
        public virtual async Task<string> ProcessCredentialRequestAsync(IAgentContext agentContext, CredentialRequestMessage
            credentialRequest, ConnectionRecord connection)
        {
            Logger.LogInformation(LoggingEvents.StoreCredentialRequest, "Type {0},", credentialRequest.Type);

            // TODO Handle case when no thread is included
            //var credential = await this.GetByThreadIdAsync(agentContext, credentialRequest.GetThreadId());

            var credential = await Policy.Handle<AriesFrameworkException>()
                .RetryAsync(3, async (ex, retry) => { await Task.Delay((int)Math.Pow(retry, 2) * 100); })
                .ExecuteAsync(() => this.GetByThreadIdAsync(agentContext, credentialRequest.GetThreadId()));

            var credentialAttachment = credentialRequest.Requests.FirstOrDefault(x => x.Id == "libindy-cred-request-0")
                                       ?? throw new ArgumentException("Credential request attachment not found.");
            if (credential.State != CredentialState.Offered)
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Credential state was invalid. Expected '{CredentialState.Offered}', found '{credential.State}'");
            credential.RequestJson = credentialAttachment.Data.Base64.GetBytesFromBase64().GetUTF8String();
            credential.ConnectionId = connection?.Id;
            await credential.TriggerAsync(CredentialTrigger.Request);
            await RecordService.UpdateAsync(agentContext.AriesStorage, credential);
            EventAggregator.Publish(new ServiceMessageProcessingEvent
            {
                RecordId = credential.Id,
                MessageType = credentialRequest.Type,
                ThreadId = credentialRequest.GetThreadId()
            });
            return credential.Id;
        }

        /// <inheritdoc />
        public Task<(CredentialIssueMessage, CredentialRecord)> CreateCredentialAsync(IAgentContext agentContext, string
            credentialId)
        {
            return CreateCredentialAsync(agentContext, credentialId, values: null);
        }

        /// <inheritdoc />
        public async Task<(CredentialIssueMessage, CredentialRecord)> CreateCredentialAsync(IAgentContext agentContext,
            string credentialId, IEnumerable<CredentialPreviewAttribute> values)
        {
            var credentialRecord = await GetAsync(agentContext, credentialId);
            if (credentialRecord.State != CredentialState.Requested)
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Credential state was invalid. Expected '{CredentialState.Requested}', found '{credentialRecord.State}'");
            if (values != null && values.Any())
                credentialRecord.CredentialAttributesValues = values;

            var definitionRecord =
                await SchemaService.GetCredentialDefinitionAsync(agentContext.AriesStorage,
                    credentialRecord.CredentialDefinitionId);
            if (credentialRecord.ConnectionId != null)
            {
                var connection = await ConnectionService.GetAsync(agentContext, credentialRecord.ConnectionId);
                if (connection.State != ConnectionState.Connected)
                    throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                        $"Connection state was invalid. Expected '{ConnectionState.Connected}', found '{connection.State}'");
            }

            (AriesIssuerCreateCredentialResult issuedCredential, RevocationRegistryRecord revocationRecord) = 
                await IssueCredentialSafeAsync(
                    agentContext, 
                    definitionRecord,
                    credentialRecord);

            if (definitionRecord.SupportsRevocation)
            {
                var provisioning = await ProvisioningService.GetProvisioningAsync(agentContext.AriesStorage);
                var paymentInfo =
                    await PaymentService.GetTransactionCostAsync(agentContext, TransactionTypes.REVOC_REG_ENTRY);

                if (issuedCredential.RevocRegDeltaJson != null)
                {
                    await LedgerService.SendRevocationRegistryEntryAsync(
                        context: agentContext,
                        issuerDid: provisioning.IssuerDid,
                        revocationRegistryDefinitionId: revocationRecord.Id,
                        revocationDefinitionType: "CL_ACCUM",
                        value: issuedCredential.RevocRegDeltaJson,
                        paymentInfo: paymentInfo);
                }

                // Store data relevant for credential revocation
                credentialRecord.CredentialRevocationId = issuedCredential.RevocId;
                credentialRecord.RevocationRegistryId = revocationRecord.Id;

                if (paymentInfo != null)
                {
                    await RecordService.UpdateAsync(agentContext.AriesStorage, paymentInfo.PaymentAddress);
                }
            }

            await credentialRecord.TriggerAsync(CredentialTrigger.Issue);
            await RecordService.UpdateAsync(agentContext.AriesStorage, credentialRecord);
            var threadId = credentialRecord.GetTag(TagConstants.LastThreadId);

            var credentialMsg = new CredentialIssueMessage(agentContext.UseMessageTypesHttps)
            {
                Credentials = new[]
                {
                    new Attachment
                    {
                        Id = "libindy-cred-0",
                        MimeType = CredentialMimeTypes.ApplicationJsonMimeType,
                        Data = new AttachmentContent
                        {
                            Base64 = issuedCredential.CredentialJson
                                .GetUTF8Bytes()
                                .ToBase64String()
                        }
                    }
                }
            };

            credentialMsg.ThreadFrom(threadId);
            return (credentialMsg, credentialRecord);
        }

        private async Task<(AriesIssuerCreateCredentialResult, RevocationRegistryRecord)> IssueCredentialSafeAsync(
            IAgentContext agentContext,
            DefinitionRecord definitionRecord,
            CredentialRecord credentialRecord)
        {
            RevocationRegistryRecord revocationRecord = null;
            if (definitionRecord.SupportsRevocation)
            {
                revocationRecord =
                    await RecordService.GetAsync<RevocationRegistryRecord>(agentContext.AriesStorage,
                        definitionRecord.CurrentRevocationRegistryId);
            }

            var (attrNames, attrNamesRaw, attrNamesEnc) = CredentialUtils.FormatCredentialValuesForIndySharedRs(credentialRecord.CredentialAttributesValues);
            List<long> regUsed = new(); /** TODO : ??? - Where to get this parameter? **/
            string credentialJson, revocationRegistryUpdatedJson, revocationRegistryDeltaJson;

            try
            {
                long.TryParse(definitionRecord.CurrentRevocationRegistryId.Split(':').LastOrDefault()?.Split('-').FirstOrDefault(), out long revRegistryIndex);

                (credentialJson, revocationRegistryUpdatedJson, revocationRegistryDeltaJson) = await IndySharedRsCred.CreateCredentialAsync(
                    credentialRecord.GetTag(TagConstants.CredDefJson),
                    credentialRecord.GetTag(TagConstants.CredDefPrivateJson),
                    credentialRecord.OfferJson,
                    credentialRecord.RequestJson,
                    attrNames,
                    attrNamesRaw,
                    attrNamesEnc,
                    revocationRecord.GetTag(TagConstants.RevRegDefJson),
                    revocationRecord.GetTag(TagConstants.RevRegDefPrivateJson),
                    revocationRecord.GetTag(TagConstants.RevRegJson),
                    revRegistryIndex,
                    regUsed);

                /** TODO : ??? - need to update revRegJson info , see indy-sdk : indy_issuer_create_credential **/
                revocationRecord.SetTag(TagConstants.RevRegJson, revocationRegistryUpdatedJson);
                await RecordService.UpdateAsync(agentContext.AriesStorage, revocationRecord);

                return (
                    new AriesIssuerCreateCredentialResult(
                        credentialJson : credentialJson,
                        revocId : await IndySharedRsCred.GetCredentialAttributeAsync(credentialJson, "rev_reg_id"),
                        revocRegDeltaJson : revocationRegistryDeltaJson
                        ),
                    revocationRecord);
            }
            catch (RevocationRegistryFullException)
            {
                if (!definitionRecord.RevocationAutoScale) throw;
            }

            var registryIndex = definitionRecord.CurrentRevocationRegistryId.Split(':').LastOrDefault()?.Split('-')
                .FirstOrDefault();

            string registryTag;
            if (int.TryParse(registryIndex, out var currentIndex))
            {
                registryTag = $"{currentIndex + 1}-{definitionRecord.MaxCredentialCount}";
            }
            else
            {
                registryTag = $"1-{definitionRecord.MaxCredentialCount}";
            }

            var (revocationRegistryResult, nextRevocationRecord) = await SchemaService.CreateRevocationRegistryAsync(agentContext, registryTag,
                definitionRecord);

            definitionRecord.CurrentRevocationRegistryId = nextRevocationRecord.Id;
            await RecordService.UpdateAsync(agentContext.AriesStorage, definitionRecord);
            long.TryParse(revocationRegistryResult.RevRegId.Split(':').LastOrDefault()?.Split('-').FirstOrDefault(), out var revRegistryNewIndex);

            (credentialJson, revocationRegistryUpdatedJson, revocationRegistryDeltaJson) = await IndySharedRsCred.CreateCredentialAsync(
                definitionRecord.GetTag(TagConstants.CredDefJson),
                definitionRecord.GetTag(TagConstants.CredDefPrivateJson),
                credentialRecord.OfferJson,
                credentialRecord.RequestJson,
                attrNames,
                attrNamesRaw,
                attrNamesEnc,
                revocationRegistryResult.RevRegDefJson,
                revocationRegistryResult.RevRegDefPvtJson,
                revocationRegistryResult.RevRegEntryJson,
                revRegistryNewIndex,
                regUsed
                );

            /** TODO : ??? - need to update revRegJson info , see indy-sdk : indy_issuer_create_credential **/
            nextRevocationRecord.SetTag(TagConstants.RevRegJson, revocationRegistryUpdatedJson);
            await RecordService.UpdateAsync(agentContext.AriesStorage, nextRevocationRecord);

            return (
                new AriesIssuerCreateCredentialResult(
                    credentialJson: credentialJson, 
                    revocId: await IndySharedRsCred.GetCredentialAttributeAsync(credentialJson, "rev_reg_id"),  //Alternative: revocationRegistryResult.RevRegId
                    revocRegDeltaJson: revocationRegistryDeltaJson),
                nextRevocationRecord);
        }
    }
}
