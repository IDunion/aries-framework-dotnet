using anoncreds_rs_dotnet.Models;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Decorators;
using Hyperledger.Aries.Decorators.Attachments;
using Hyperledger.Aries.Decorators.PleaseAck;
using Hyperledger.Aries.Decorators.Service;
using Hyperledger.Aries.Decorators.Threading;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.Handshakes.Common;
using Hyperledger.Aries.Features.Handshakes.Connection;
using Hyperledger.Aries.Features.IssueCredential.Models;
using Hyperledger.Aries.Features.IssueCredential.Models.Messages;
using Hyperledger.Aries.Features.IssueCredential.Records;
using Hyperledger.Aries.Features.RevocationNotification;
using Hyperledger.Aries.Ledger.Models;
using Hyperledger.Aries.Models.Events;
using Hyperledger.Aries.Models.Records;
using Hyperledger.Aries.Payments;
using Hyperledger.Aries.Payments.Models;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Hyperledger.Aries.Common.AnoncredsModelExtensions;
using Anoncreds = anoncreds_rs_dotnet.Anoncreds;

namespace Hyperledger.Aries.Features.IssueCredential
{
    /// <inheritdoc />
    public class DefaultCredentialServiceV2 : ICredentialService
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
        protected readonly ILogger<DefaultCredentialServiceV2> Logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultCredentialServiceV2"/> class.
        /// </summary>
        /// <param name="eventAggregator">The event aggregator.</param>
        /// <param name="ledgerService">The ledger service.</param>
        /// <param name="connectionService">The connection service.</param>
        /// <param name="recordService">The record service.</param>
        /// <param name="schemaService">The schema service.</param>
        /// <param name="provisioningService">The provisioning service.</param>
        /// <param name="paymentService">The payment service.</param>
        /// <param name="messageService">The message service</param>
        /// <param name="logger">The logger.</param>
        public DefaultCredentialServiceV2(
            IEventAggregator eventAggregator,
            ILedgerService ledgerService,
            IConnectionService connectionService,
            IWalletRecordService recordService,
            ISchemaService schemaService,
            IProvisioningService provisioningService,
            IPaymentService paymentService,
            IMessageService messageService,
            ILogger<DefaultCredentialServiceV2> logger)
        {
            EventAggregator = eventAggregator;
            LedgerService = ledgerService;
            ConnectionService = connectionService;
            RecordService = recordService;
            SchemaService = schemaService;
            ProvisioningService = provisioningService;
            PaymentService = paymentService;
            MessageService = messageService;
            Logger = logger;
        }

        /// <inheritdoc />
        public virtual async Task<CredentialRecord> GetAsync(IAgentContext agentContext, string credentialId)
        {
            CredentialRecord record = await RecordService.GetAsync<CredentialRecord>(agentContext.AriesStorage, credentialId);

            return record ?? throw new AriesFrameworkException(ErrorCode.RecordNotFound, "Credential record not found");
        }

        /// <inheritdoc />
        public virtual Task<List<CredentialRecord>> ListAsync(IAgentContext agentContext, ISearchQuery query = null,
            int count = 100, int skip = 0)
        {
            return RecordService.SearchAsync<CredentialRecord>(agentContext.AriesStorage, query, null, count, skip);
        }

        /// <inheritdoc />
        public virtual async Task RejectOfferAsync(IAgentContext agentContext, string offerId)
        {
            CredentialRecord credential = await GetAsync(agentContext, offerId);

            if (credential.State != CredentialState.Offered)
            {
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Credential state was invalid. Expected '{CredentialState.Offered}', found '{credential.State}'");
            }

            await credential.TriggerAsync(CredentialTrigger.Reject);
            await RecordService.UpdateAsync(agentContext.AriesStorage, credential);
        }

        /// <inheritdoc />
        public async Task RevokeCredentialOfferAsync(IAgentContext agentContext, string offerId)
        {
            CredentialRecord credentialRecord = await GetAsync(agentContext, offerId);

            if (credentialRecord.State != CredentialState.Offered)
            {
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Credential state was invalid. Expected '{CredentialState.Offered}', found '{credentialRecord.State}'");
            }

            _ = await RecordService.DeleteAsync<ConnectionRecord>(agentContext.AriesStorage, offerId);
        }

        /// <inheritdoc />
        public virtual async Task RejectCredentialRequestAsync(IAgentContext agentContext, string credentialId)
        {
            CredentialRecord credential = await GetAsync(agentContext, credentialId);

            if (credential.State != CredentialState.Requested)
            {
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Credential state was invalid. Expected '{CredentialState.Requested}', found '{credential.State}'");
            }

            await credential.TriggerAsync(CredentialTrigger.Reject);
            await RecordService.UpdateAsync(agentContext.AriesStorage, credential);
        }

        /// <inheritdoc />
        public virtual async Task RevokeCredentialAsync(IAgentContext agentContext, string credentialId, bool sendRevocationNotification = false)
        {
            CredentialRecord credentialRecord = await GetAsync(agentContext, credentialId);

            if (credentialRecord.State != CredentialState.Issued)
            {
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Credential state was invalid. Expected '{CredentialState.Issued}', found '{credentialRecord.State}'");
            }

            ProvisioningRecord provisioning = await ProvisioningService.GetProvisioningAsync(agentContext.AriesStorage);

            // Check if the state machine is valid for revocation
            await credentialRecord.TriggerAsync(CredentialTrigger.Revoke);

            RevocationRegistryRecord revocationRecord =
                await RecordService.GetAsync<RevocationRegistryRecord>(agentContext.AriesStorage,
                    credentialRecord.RevocationRegistryId);

            // Revoke the credential
            long credRevIdx = 0;
            if (credentialRecord.CredentialRevocationId != null)
            {
                _ = long.TryParse(credentialRecord.CredentialRevocationId, out credRevIdx);
            }
            var tailsPath = await Anoncreds.RevocationApi.GetRevocationRegistryDefinitionAttributeAsync(revocationRecord.RevRegDefJson, "tails_location");

            //TODO : wait for anoncreds-rs Revocation to be finished and tested
            long.TryParse(JObject.Parse(revocationRecord.RevStatusListJson)["timestamp"].ToString(), out var revStatusListTimestamp);

            //(string revRegUpdatedJson, string revocRegistryDeltaJson) = await Anoncreds.RevocationApi.UpdateRevocationStatusListJsonAsync(
            string revocRegistryDeltaJson = "";
            string newRevocationStatusList = await Anoncreds.RevocationApi.UpdateRevocationStatusListJsonAsync(
                timestamp: revStatusListTimestamp,
                new List<long> {0}, // Todo
                new List<long> {0}, // Todo
                revRegDefJson: revocationRecord.RevRegDefJson,
                currentRevStatusListJson: revocationRecord.RevStatusListJson);

            if ((string)JObject.Parse(revocationRecord.RevRegDefJson)["value"]["issuanceType"] == IssuerType.ISSUANCE_BY_DEFAULT.ToString())
            {
                if (revocationRecord.CredRevocationIdxUsed == null)
                {
                    revocationRecord.CredRevocationIdxUsed = new List<long> { credRevIdx };
                }
                else
                {
                    revocationRecord.CredRevocationIdxUsed.Add(credRevIdx);
                }
            }
            revocationRecord.RevRegJson = JObject.Parse(revocationRecord.RevStatusListJson)["registry"].ToString();
            revocationRecord.RevStatusListJson = newRevocationStatusList;

            TransactionCost paymentInfo =
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
            {
                await RecordService.UpdateAsync(agentContext.AriesStorage, paymentInfo.PaymentAddress);
            }

            // Update local credential record
            await RecordService.UpdateAsync(agentContext.AriesStorage, credentialRecord);
            // Update local revocation record
            await RecordService.UpdateAsync(agentContext.AriesStorage, revocationRecord);

            if (!sendRevocationNotification)
            {
                return;
            }

            ConnectionRecord connection = await ConnectionService.GetAsync(agentContext, credentialRecord.ConnectionId);

            Logger.LogInformation($"Sending Revocation Notification for credential {credentialId} to {connection.Endpoint}...");

            RevocationNotificationMessage revocationNotificationMessage = new()
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
            _ = await RecordService.DeleteAsync<CredentialRecord>(agentContext.AriesStorage, credentialId);
        }

        /// <inheritdoc />
        public async Task<CredentialAcknowledgeMessage> CreateAcknowledgementMessageAsync(IAgentContext agentContext, string credentialRecordId,
            string status = AcknowledgementStatusConstants.Ok)
        {
            CredentialRecord record = await GetAsync(agentContext, credentialRecordId);

            string threadId = record.GetTag(TagConstants.LastThreadId);
            CredentialAcknowledgeMessage acknowledgeMessage = new(agentContext.UseMessageTypesHttps)
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
            CredentialRecord credentialRecord = await this.GetByThreadIdAsync(agentContext, credentialAcknowledgeMessage.GetThreadId());

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
            Attachment offerAttachment = credentialOffer.Offers.FirstOrDefault(x => x.Id == "libindy-cred-offer-0")
                                  ?? throw new ArgumentNullException(nameof(CredentialOfferMessage.Offers));

            string offerJson = offerAttachment.Data.Base64.GetBytesFromBase64().GetUTF8String();
            JObject offer = JObject.Parse(offerJson);
            string definitionId = offer["cred_def_id"].ToObject<string>();
            string schemaId = offer["schema_id"].ToObject<string>();

            string threadId = credentialOffer.GetThreadId() ?? Guid.NewGuid().ToString();
            // Write offer record to local wallet
            CredentialRecord credentialRecord = new()
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
        public async Task<(CredentialRequestMessage, CredentialRecord)> CreateRequestAsync(IAgentContext agentContext,
            string credentialId)
        {
            CredentialRecord credential = await GetAsync(agentContext, credentialId);
            if (credential.State != CredentialState.Offered)
            {
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Credential state was invalid. Expected '{CredentialState.Offered}', found '{credential.State}'");
            }

            string proverDid;
            if (credential.ConnectionId != null)
            {
                ConnectionRecord connection = await ConnectionService.GetAsync(agentContext, credential.ConnectionId);
                proverDid = connection.MyDid;
            }

            else
            {
                (string newDid, _) = await DidUtils.CreateAndStoreMyDidAsync(agentContext.AriesStorage, RecordService);
                proverDid = newDid;
            }

            AriesResponse definition = await LedgerService.LookupDefinitionAsync(agentContext, credential.CredentialDefinitionId);
            ProvisioningRecord provisioning = await ProvisioningService.GetProvisioningAsync(agentContext.AriesStorage);

            //Need to replace schemaId as seqNo with schemaId as id string for indy-shared-rs method
            (definition.ObjectJson, string schemaId) = await ReplaceSchemaIdSeqNoWithString(agentContext, definition.ObjectJson, credential.CredentialDefinitionId);

            //workaround 
            definition.ObjectJson = definition.ObjectJson.ToAnoncredsJson(AnoncredsModel.CredDef);
            //
            (string CredentialRequestJson, string CredentialRequestMetadataJson) = await Anoncreds.CredentialRequestApi.CreateCredentialRequestJsonAsync(
                entropy: null,
                proverDid: proverDid,
                credentialDefinitionJson: definition.ObjectJson,
                masterSecretJson: await MasterSecretUtils.GetMasterSecretJsonAsync(agentContext.AriesStorage, RecordService, provisioning.MasterSecretId),
                masterSecretId: provisioning.MasterSecretId,
                credentialOfferJson: credential.OfferJson
                );

            credential.SchemaId = schemaId;

            // Update local credential record with new info
            credential.CredentialRequestMetadataJson = CredentialRequestMetadataJson;
            await credential.TriggerAsync(CredentialTrigger.Request);
            await RecordService.UpdateAsync(agentContext.AriesStorage, credential);
            string threadId = credential.GetTag(TagConstants.LastThreadId);

            CredentialRequestMessage response = new(agentContext.UseMessageTypesHttps)
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
            Attachment credentialAttachment = credential.Credentials.FirstOrDefault(x => x.Id == "libindy-cred-0")
                                       ?? throw new ArgumentException("Credential attachment not found");

            string credentialJson = credentialAttachment.Data.Base64.GetBytesFromBase64().GetUTF8String();
            JObject credentialJobj = JObject.Parse(credentialJson);
            string definitionId = credentialJobj["cred_def_id"].ToObject<string>();
            string revRegId = credentialJobj["rev_reg_id"]?.ToObject<string>();

            CredentialRecord credentialRecord = await Policy.Handle<AriesFrameworkException>()
                .RetryAsync(3, async (ex, retry) => { await Task.Delay((int)Math.Pow(retry, 2) * 100); })
                .ExecuteAsync(() => this.GetByThreadIdAsync(agentContext, credential.GetThreadId()));

            if (credentialRecord.State != CredentialState.Requested)
            {
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Credential state was invalid. Expected '{CredentialState.Requested}', found '{credentialRecord.State}'");
            }

            AriesResponse credentialDefinition = await LedgerService.LookupDefinitionAsync(agentContext, definitionId);

            string revocationRegistryDefinitionJson = null;
            if (!string.IsNullOrEmpty(revRegId))
            {
                // If credential supports revocation, lookup registry definition
                AriesResponse revocationRegistry =
                    await LedgerService.LookupRevocationRegistryDefinitionAsync(agentContext, revRegId);
                revocationRegistryDefinitionJson = revocationRegistry.ObjectJson;
                credentialRecord.RevocationRegistryId = revRegId;
            }

            ProvisioningRecord provisioning = await ProvisioningService.GetProvisioningAsync(agentContext.AriesStorage);

            //Need to replace schemaId as seqNo with schemaId as id string for indy-shared-rs method
            (credentialDefinition.ObjectJson, string schemaId) = await ReplaceSchemaIdSeqNoWithString(agentContext, credentialDefinition.ObjectJson, credentialDefinition.Id);
            string masterSecretJson = await MasterSecretUtils.GetMasterSecretJsonAsync(agentContext.AriesStorage, RecordService, provisioning.MasterSecretId);

            //workaround
            credentialDefinition.ObjectJson = credentialDefinition.ObjectJson.ToAnoncredsJson(AnoncredsModel.CredDef);
            //

            string credentialProcessedJson = await Anoncreds.CredentialApi.ProcessCredentialAsync(
                credentialJson,
                credentialRecord.CredentialRequestMetadataJson,
                masterSecretJson,
                credentialDefinition.ObjectJson,
                revocationRegistryDefinitionJson
                );
            string credentialProcessedId = await Anoncreds.CredentialApi.GetCredentialAttributeAsync(credentialProcessedJson, "cred_def_id");

            JObject credJObject = JObject.Parse(credentialProcessedJson);
            try
            {
                _ = (long)credJObject["schema_id"];
                credJObject["schema_id"] = schemaId;
            }
            catch
            {
                //schema id is already a string
            }
            credentialRecord.CredentialJson = JsonConvert.SerializeObject(credJObject);
            credentialRecord.SchemaId = schemaId;

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

            string threadId = Guid.NewGuid().ToString();
            if (!string.IsNullOrEmpty(connectionId))
            {
                ConnectionRecord connection = await ConnectionService.GetAsync(agentContext, connectionId);

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
            (string credDefJson, string schemaId) = await ReplaceSchemaIdSeqNoWithString(agentContext, definition.CredDefJson, config.CredentialDefinitionId);

            string offerJson = await Anoncreds.CredentialOfferApi.CreateCredentialOfferJsonAsync(
                schemaId,
                definition.Id,
                definition.KeyCorrectnesProofJson);

            // Write offer record to local wallet
            CredentialRecord credentialRecord = new()
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
            {
                credentialRecord.SetTag(TagConstants.IssuerDid, config.IssuerDid);
            }

            if (config.Tags != null)
            {
                foreach (KeyValuePair<string, string> tag in config.Tags)
                {
                    if (!credentialRecord.Tags.Keys.Contains(tag.Key))
                    {
                        credentialRecord.Tags.Add(tag.Key, tag.Value);
                    }
                }
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

            (CredentialOfferMessage message, CredentialRecord record) = await CreateOfferAsync(agentContext, config, null);
            ProvisioningRecord provisioning = await ProvisioningService.GetProvisioningAsync(agentContext.AriesStorage);
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

            CredentialRecord credential = await Policy.Handle<AriesFrameworkException>()
                .RetryAsync(3, async (ex, retry) => { await Task.Delay((int)Math.Pow(retry, 2) * 100); })
                .ExecuteAsync(() => this.GetByThreadIdAsync(agentContext, credentialRequest.GetThreadId()));

            Attachment credentialAttachment = credentialRequest.Requests.FirstOrDefault(x => x.Id == "libindy-cred-request-0")
                                       ?? throw new ArgumentException("Credential request attachment not found.");
            if (credential.State != CredentialState.Offered)
            {
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Credential state was invalid. Expected '{CredentialState.Offered}', found '{credential.State}'");
            }

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
        public async Task<CredentialRecord> CreateCredentialAsync(IAgentContext agentContext,
            CredentialOfferMessage message)
        {
            string credentialRecordId = "";
            try
            {
                ServiceDecorator service = message.GetDecorator<ServiceDecorator>(DecoratorNames.ServiceDecorator);

                credentialRecordId = await ProcessOfferAsync(agentContext, message, null);

                (CredentialRequestMessage request, CredentialRecord record) = await CreateRequestAsync(agentContext, credentialRecordId);
                ProvisioningRecord provisioning = await ProvisioningService.GetProvisioningAsync(agentContext.AriesStorage);

                try
                {
                    CredentialIssueMessage credentialIssueMessage = await MessageService.SendReceiveAsync<CredentialIssueMessage>(
                        agentContext: agentContext,
                        message: request,
                        recipientKey: service.RecipientKeys.First(),
                        endpointUri: service.ServiceEndpoint,
                        routingKeys: service.RoutingKeys.ToArray(),
                        senderKey: provisioning.IssuerVerkey);
                    string recordId = await ProcessCredentialAsync(agentContext, credentialIssueMessage, null);
                    return await RecordService.GetAsync<CredentialRecord>(agentContext.AriesStorage, recordId);
                }
                catch (AriesFrameworkException ex) when (ex.ErrorCode == ErrorCode.A2AMessageTransmissionError)
                {
                    throw new AriesFrameworkException(ex.ErrorCode, ex.Message, record, null);
                }
            }
            catch (Exception e)
            {
                throw new AriesFrameworkException(ErrorCode.LedgerItemNotFound, e.Message + ": " + credentialRecordId);
            }
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
            CredentialRecord credentialRecord = await GetAsync(agentContext, credentialId);
            if (credentialRecord.State != CredentialState.Requested)
            {
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Credential state was invalid. Expected '{CredentialState.Requested}', found '{credentialRecord.State}'");
            }

            if (values != null && values.Any())
            {
                credentialRecord.CredentialAttributesValues = values;
            }

            DefinitionRecord definitionRecord =
                await SchemaService.GetCredentialDefinitionAsync(agentContext.AriesStorage,
                    credentialRecord.CredentialDefinitionId);
            if (credentialRecord.ConnectionId != null)
            {
                ConnectionRecord connection = await ConnectionService.GetAsync(agentContext, credentialRecord.ConnectionId);
                if (connection.State != ConnectionState.Connected)
                {
                    throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                        $"Connection state was invalid. Expected '{ConnectionState.Connected}', found '{connection.State}'");
                }
            }

            (AriesIssuerCreateCredentialResult issuedCredential, RevocationRegistryRecord revocationRecord) =
                await IssueCredentialSafeAsync(
                    agentContext,
                    definitionRecord,
                    credentialRecord);

            if (definitionRecord.SupportsRevocation)
            {
                ProvisioningRecord provisioning = await ProvisioningService.GetProvisioningAsync(agentContext.AriesStorage);
                TransactionCost paymentInfo =
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
            string threadId = credentialRecord.GetTag(TagConstants.LastThreadId);

            CredentialIssueMessage credentialMsg = new(agentContext.UseMessageTypesHttps)
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
            string credentialJson;
            string revocationRegistryUpdatedJson = "";
            string revocationRegistryDeltaJson = "";

            (List<string> attrNames, List<string> attrNamesRaw, List<string> attrNamesEnc) = CredentialUtils.FormatCredentialValuesForAnoncreds(credentialRecord.CredentialAttributesValues);

            //Default values for use without revocation
            long revRegistryIndex = 0;
            string revRegDefJson = null;
            string revRegDefPrivateJson = null;
            string revRegJson = null;
            long credRevocationIdx = -1;
            List<long> credRevocationIdxUsed = null;
            string revocationStateListJson = null;
            string credDefFormatAnoncredsJson = null;

            try
            {
                if (revocationRecord != null)
                {
                    if (definitionRecord.CurrentRevocationRegistryId != null)
                    {
                        _ = long.TryParse(definitionRecord.CurrentRevocationRegistryId.Split(':').LastOrDefault()?.Split('-').FirstOrDefault(), out revRegistryIndex);
                    }

                    revRegDefJson = revocationRecord.RevRegDefJson;
                    revRegDefPrivateJson = revocationRecord.RevRegDefPrivateJson;
                    revRegJson = revocationRecord.RevRegJson;
                    credRevocationIdx = revocationRecord.NextCredRevocationIdx;
                    credRevocationIdxUsed = revocationRecord.CredRevocationIdxUsed;
                    revocationStateListJson = revocationRecord.RevStatusListJson;

                    //Check if RevocationRegistry has enough space for a new credential.
                    if (!long.TryParse(await Anoncreds.RevocationApi.GetRevocationRegistryDefinitionAttributeAsync(revRegDefJson, "max_cred_num"), out long maxCredNum))
                    {
                        throw new AriesFrameworkException(ErrorCode.InvalidRecordData, "Invalid Parameter max_cred_num.");
                    }

                    if (revocationRecord.NextCredRevocationIdx > maxCredNum)
                    {
                        throw new AriesFrameworkException(ErrorCode.RevocationRegistryFull, $"Revocation registry with index {revRegistryIndex} is full (Max credential number is {maxCredNum}). If RevocationAutoScale is activated a new one will be created and index is incremented by 1.");
                    }
                }

                //workaround
                credDefFormatAnoncredsJson = definitionRecord.CredDefJson.ToAnoncredsJson(AnoncredsModel.CredDef);
                //

                credentialJson = await Anoncreds.CredentialApi.CreateCredentialAsync(
                    credDefObjectJson : credDefFormatAnoncredsJson,
                    credDefPvtObjectJson: definitionRecord.PrivateJson,
                    credOfferObjectJson : credentialRecord.OfferJson,
                    credReqObjectJson : credentialRecord.RequestJson,
                    attributeNames : attrNames,
                    attributeRawValues : attrNamesRaw,
                    attributeEncodedValues : attrNamesEnc,
                    revocationRegistryDefinitionJson : revRegDefJson,
                    revocationRegistryDefinitionPrivateJson : revRegDefPrivateJson,
                    revocationRegistryId : null, //Todo : how to use this field? indicates if use with old (Deltas) or new (RevStatusLists) revocation standard?
                    revStatusListObjectJson : revocationStateListJson,
                    regIdx : credRevocationIdx);
                
                if (revocationRecord != null)
                {
                    credRevocationIdx = await UpdateCredentialRevocationRegistryIdxAndUsedIdx(revocationRecord, revRegDefJson);
                    revocationRecord.RevRegJson = JObject.Parse(revocationStateListJson)["registry"].ToString();
                    await RecordService.UpdateAsync(agentContext.AriesStorage, revocationRecord);
                }

                return (
                    new AriesIssuerCreateCredentialResult(
                        credentialJson: credentialJson,
                        revocId: credRevocationIdx != -1 ? credRevocationIdx.ToString() : null,
                        revocRegDeltaJson: revocationRegistryDeltaJson
                        ),
                    revocationRecord);
            }
            catch (AriesFrameworkException e)
            {
                if (e.ErrorCode != ErrorCode.RevocationRegistryFull)
                {
                    throw;
                }
                if (!definitionRecord.RevocationAutoScale)
                {
                    throw;
                }
            }

            string registryIndex = definitionRecord.CurrentRevocationRegistryId.Split(':').LastOrDefault()?.Split('-')
                .FirstOrDefault();

            string registryTag = int.TryParse(registryIndex, out int currentIndex)
                ? $"{currentIndex + 1}-{definitionRecord.MaxCredentialCount}"
                : $"1-{definitionRecord.MaxCredentialCount}";
            (RevocationRegistryResult revocationRegistryResult, RevocationRegistryRecord nextRevocationRecord) = await SchemaService.CreateRevocationRegistryAsync(agentContext, registryTag,
                definitionRecord);

            definitionRecord.CurrentRevocationRegistryId = nextRevocationRecord.Id;
            await RecordService.UpdateAsync(agentContext.AriesStorage, definitionRecord);

            //workaround
            credDefFormatAnoncredsJson = definitionRecord.CredDefJson.ToAnoncredsJson(AnoncredsModel.CredDef);
            //

            credentialJson = await Anoncreds.CredentialApi.CreateCredentialAsync(
                credDefObjectJson: credDefFormatAnoncredsJson,
                credDefPvtObjectJson: definitionRecord.PrivateJson,
                credOfferObjectJson: credentialRecord.OfferJson,
                credReqObjectJson: credentialRecord.RequestJson,
                attributeNames: attrNames,
                attributeRawValues: attrNamesRaw,
                attributeEncodedValues: attrNamesEnc,
                revocationRegistryDefinitionJson : revocationRegistryResult.RevRegDefJson,
                revocationRegistryDefinitionPrivateJson : revocationRegistryResult.RevRegDefPvtJson,
                revocationRegistryId : null, //Todo : how to use this field? indicates if use with old (Deltas) or new (RevStatusLists) revocation standard?
                revStatusListObjectJson : nextRevocationRecord.RevStatusListJson,
                regIdx : nextRevocationRecord.NextCredRevocationIdx);

            credRevocationIdx = await UpdateCredentialRevocationRegistryIdxAndUsedIdx(nextRevocationRecord, revocationRegistryResult.RevRegDefJson);
            nextRevocationRecord.RevRegJson = revocationRegistryUpdatedJson;
            await RecordService.UpdateAsync(agentContext.AriesStorage, nextRevocationRecord);

            return (
                new AriesIssuerCreateCredentialResult(
                    credentialJson: credentialJson,
                    revocId: credRevocationIdx.ToString(),
                    revocRegDeltaJson: revocationRegistryDeltaJson),
                nextRevocationRecord);
        }

        /// <summary>
        /// Converts the schemaId in CredDefJson from seqNo to id string. 
        /// </summary>
        /// <param name="agentContext">Agent context.</param>
        /// <param name="credDefJson">Json of credential definition.</param>
        /// <param name="credDefId">Credential definition id of which we get the schema id.</param>
        /// <returns>Credential defintion with replaced schema id.</returns>
        private async Task<(string, string)> ReplaceSchemaIdSeqNoWithString(IAgentContext agentContext, string credDefJson, string credDefId)
        {
            string schemaJson = await SchemaService.LookupSchemaFromCredentialDefinitionAsync(agentContext, credDefId);
            JObject jresponseSchema = JObject.Parse(schemaJson);
            string schemaId = jresponseSchema["id"].ToString();

            JObject jresponseCredDef = JObject.Parse(credDefJson);
            jresponseCredDef["schemaId"].Replace(schemaId);
            return (jresponseCredDef.ToString(), schemaId);
        }

        /// <summary>
        /// Updates the NextCredRevocationRegistryIdx in revocation record and returns the credential revocation index for a created credential. If Issuance on demand was used, adds the credential revocation index to the CredRevocationIdxUsed list (issued list). 
        /// </summary>
        /// <returns>credential revocation index for the credential record.</returns>
        private Task<long> UpdateCredentialRevocationRegistryIdxAndUsedIdx(RevocationRegistryRecord revRegRecord, string RevRegDefJson)
        {
            if ((string)JObject.Parse(RevRegDefJson)["value"]["issuanceType"] == IssuerType.ISSUANCE_ON_DEMAND.ToString())
            {
                if (revRegRecord.CredRevocationIdxUsed == null)
                {
                    revRegRecord.CredRevocationIdxUsed = new List<long> { revRegRecord.NextCredRevocationIdx };
                }
                else
                {
                    revRegRecord.CredRevocationIdxUsed.Add(revRegRecord.NextCredRevocationIdx);
                }
            }

            long credRevocationRegistryIdx = revRegRecord.NextCredRevocationIdx;
            revRegRecord.NextCredRevocationIdx += 1;
            return Task.FromResult(credRevocationRegistryIdx);
        }
    }
}
