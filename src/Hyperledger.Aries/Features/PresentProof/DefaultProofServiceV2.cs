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
using Hyperledger.Aries.Models.Records;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Utils;
using indy_shared_rs_dotnet.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IndySharedRsPres = indy_shared_rs_dotnet.IndyCredx.PresentationApi;
using IndySharedRsPresReq = indy_shared_rs_dotnet.IndyCredx.PresentationRequestApi;
using IndySharedRsRev = indy_shared_rs_dotnet.IndyCredx.RevocationApi;

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
        /// Message Service
        /// </summary>
        protected readonly IMessageService MessageService;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultProofServiceV2"/> class.
        /// </summary>
        /// <param name="eventAggregator">The event aggregator.</param>
        /// <param name="connectionService">The connection service.</param>
        /// <param name="recordService">The record service.</param>
        /// <param name="provisioningService">The provisioning service.</param>
        /// <param name="ledgerService">The ledger service.</param>
        /// <param name="messageService">The message service.</param>
        /// <param name="logger">The logger.</param>
        public DefaultProofServiceV2(
            IEventAggregator eventAggregator,
            IConnectionService connectionService,
            IWalletRecordService recordService,
            IProvisioningService provisioningService,
            ILedgerService ledgerService,
            IMessageService messageService,
            ILogger<DefaultProofServiceV2> logger)
        {
            EventAggregator = eventAggregator;
            MessageService = messageService;
            ConnectionService = connectionService;
            RecordService = recordService;
            ProvisioningService = provisioningService;
            LedgerService = ledgerService;
            Logger = logger;
        }

        /// <inheritdoc />
        public virtual async Task<string> CreateProofAsync(IAgentContext agentContext,
            ProofRequest proofRequest, RequestedCredentials requestedCredentials)
        {
            ProvisioningRecord provisioningRecord = await ProvisioningService.GetProvisioningAsync(agentContext.AriesStorage);

            List<CredentialInfo> credentialObjects = new();
            List<string> credentialEntryJsons = new();
            foreach (string credId in requestedCredentials.GetCredentialIdentifiers())
            {
                //TODO : ??? Test
                CredentialRecord credentialRecord = await RecordService.GetAsync<CredentialRecord>(agentContext.AriesStorage, credId);
                indy_shared_rs_dotnet.Models.Credential credential = JsonConvert.DeserializeObject<indy_shared_rs_dotnet.Models.Credential>(credentialRecord.CredentialJson);
                string recordJson = JsonConvert.SerializeObject(credentialRecord);
                CredentialInfo credentialInfo = JsonConvert.DeserializeObject<CredentialInfo>(recordJson);

                credentialObjects.Add(credentialInfo);

                RevocationRegistryRecord revRegRecord = await RecordService.GetAsync<RevocationRegistryRecord>(agentContext.AriesStorage, credentialInfo.RevocationRegistryId);
                string revRegDefJson = revRegRecord.RevRegDefJson;
                string revRegDeltaJson = revRegRecord.RevRegDeltaJson;

                string revStateJson = await IndySharedRsRev.CreateOrUpdateRevocationStateAsync(
                    revRegDefJson,
                    revRegDeltaJson,
                    0,
                    0,
                    revRegRecord.TailsLocation,
                    new CredentialRevocationState().JsonString);

                CredentialRevocationState revState = JsonConvert.DeserializeObject<CredentialRevocationState>(revStateJson);
                credentialEntryJsons.Add(JsonConvert.SerializeObject(CredentialEntry.CreateCredentialEntry(credential, revState.Timestamp, revState)));
            }

            List<string> selfAttestNames = new();
            List<string> selfAttestValues = new();
            foreach (KeyValuePair<string, string> pair in requestedCredentials.SelfAttestedAttributes)
            {
                selfAttestNames.Add(pair.Key);
                selfAttestValues.Add(pair.Value);
            }

            string presentation = await IndySharedRsPres.CreatePresentationAsync(
                proofRequest.ToJson(),
                new List<string>(),
                new List<string>(),
                selfAttestNames,
                selfAttestValues,
                await MasterSecretUtils.GetMasterSecretJsonAsync(agentContext.AriesStorage, RecordService, provisioningRecord.MasterSecretId),
                credentialObjects.Select(x => x.SchemaId).Distinct().ToList(),
                credentialObjects.Select(x => x.CredentialDefinitionId).Distinct().ToList());

            return presentation;
        }

        /// <inheritdoc />
        public virtual async Task<ProofRecord> CreatePresentationAsync(IAgentContext agentContext, RequestPresentationMessage requestPresentation, RequestedCredentials requestedCredentials)
        {
            ServiceDecorator service = requestPresentation.GetDecorator<ServiceDecorator>(DecoratorNames.ServiceDecorator);

            ProofRecord record = await ProcessRequestAsync(agentContext, requestPresentation, null);
            (PresentationMessage presentationMessage, ProofRecord proofRecord) = await CreatePresentationAsync(agentContext, record.Id, requestedCredentials);

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
            ProofRecord request = await GetAsync(agentContext, proofRequestId);

            if (request.State != ProofState.Requested)
            {
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Proof record state was invalid. Expected '{ProofState.Requested}', found '{request.State}'");
            }

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
            if (record.RevocationRegistryId == null)
            {
                return false;
            }

            if (record.State is CredentialState.Offered or CredentialState.Requested)
            {
                return false;
            }

            if (record.State is CredentialState.Revoked or CredentialState.Rejected)
            {
                return true;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            ProofRequest proofRequest = new()
            {
                Name = "revocation check",
                Version = "1.0",
                Nonce = await IndySharedRsPresReq.GenerateNonceAsync(),
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

            string proof = await CreateProofAsync(context, proofRequest, new RequestedCredentials
            {
                RequestedAttributes = new Dictionary<string, RequestedAttribute>
                {
                    { "referent1", new RequestedAttribute { CredentialId = record.CredentialId, Timestamp = now, Revealed = true } }
                }
            });

            bool isValid = await VerifyProofAsync(context, proofRequest.ToJson(), proof);

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
            PartialProof proof = JsonConvert.DeserializeObject<PartialProof>(proofJson);

            // If any values are revealed, validate encoding
            // against expected values
            if (validateEncoding && proof.RequestedProof.RevealedAttributes != null)
            {
                foreach (KeyValuePair<string, ProofAttribute> attribute in proof.RequestedProof.RevealedAttributes)
                {
                    if (!CredentialUtils.CheckValidEncoding(attribute.Value.Raw, attribute.Value.Encoded))
                    {
                        throw new AriesFrameworkException(ErrorCode.InvalidProofEncoding,
                            $"The encoded value for '{attribute.Key}' is invalid. " +
                            $"Expected '{CredentialUtils.GetEncoded(attribute.Value.Raw)}'. " +
                            $"Actual '{attribute.Value.Encoded}'");
                    }
                }
            }

            List<string> schemas = await BuildSchemasAsync(agentContext,
                proof.Identifiers
                    .Select(x => x.SchemaId)
                    .Where(x => x != null)
                    .Distinct());

            List<string> definitions = await BuildCredentialDefinitionsAsync(agentContext,
                proof.Identifiers
                    .Select(x => x.CredentialDefintionId)
                    .Where(x => x != null)
                    .Distinct());

            List<string> revocationDefinitions = await BuildRevocationRegistryDefinitionsAsync(agentContext,
                proof.Identifiers
                    .Select(x => x.RevocationRegistryId)
                    .Where(x => x != null)
                    .Distinct());

            List<string> revocationRegistries = await BuildRevocationRegistriesAsync(
                agentContext,
                proof.Identifiers.Where(x => x.RevocationRegistryId != null));

            return await IndySharedRsPres.VerifyPresentationAsync(proofJson,
                proofRequestJson,
                schemas,
                definitions,
                revocationDefinitions,
                revocationRegistries);
        }

        /// <inheritdoc />
        public virtual async Task<bool> VerifyProofAsync(IAgentContext agentContext, string proofRecordId)
        {
            ProofRecord proofRecord = await GetAsync(agentContext, proofRecordId);

            return proofRecord.State != ProofState.Accepted
                ? throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Proof record state was invalid. Expected '{ProofState.Accepted}', found '{proofRecord.State}'")
                : await VerifyProofAsync(agentContext, proofRecord.RequestJson, proofRecord.ProofJson);
        }

        /// <inheritdoc />
        public virtual Task<List<ProofRecord>> ListAsync(IAgentContext agentContext, ISearchQuery query = null,
            int count = 100)
        {
            return RecordService.SearchAsync<ProofRecord>(agentContext.AriesStorage, query, null, count);
        }

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
            List<IssueCredential.Credential> credentials = await ProverSearchCredentialsForProofRequestAsync(agentContext, proofRequest);
            IEnumerable<IssueCredential.Credential> result = from cred in credentials
                                                             where cred.CredentialInfo.Referent == attributeReferent
                                                             select cred;
            return result.ToList();
        }

        /// <inheritdoc />
        public async Task<PresentationAcknowledgeMessage> CreateAcknowledgeMessageAsync(IAgentContext agentContext, string proofRecordId, string status = AcknowledgementStatusConstants.Ok)
        {
            ProofRecord record = await GetAsync(agentContext, proofRecordId);

            string threadId = record.GetTag(TagConstants.LastThreadId);
            PresentationAcknowledgeMessage acknowledgeMessage = new(agentContext.UseMessageTypesHttps)
            {
                Id = threadId,
                Status = status
            };
            acknowledgeMessage.ThreadFrom(threadId);

            return acknowledgeMessage;
        }

        /// <inheritdoc />
        public virtual async Task<ProofRecord> ProcessAcknowledgeMessageAsync(IAgentContext agentContext, PresentationAcknowledgeMessage presentationAcknowledgeMessage)
        {
            ProofRecord proofRecord = await this.GetByThreadIdAsync(agentContext, presentationAcknowledgeMessage.GetThreadId());

            EventAggregator.Publish(new ServiceMessageProcessingEvent
            {
                RecordId = proofRecord.Id,
                MessageType = presentationAcknowledgeMessage.Type,
                ThreadId = presentationAcknowledgeMessage.GetThreadId()
            });

            return proofRecord;
        }

        /// <inheritdoc />
        public virtual async Task<(ProposePresentationMessage, ProofRecord)> CreateProposalAsync(IAgentContext agentContext, ProofProposal proofProposal, string connectionId)
        {
            Logger.LogInformation(LoggingEvents.CreateProofRequest, "ConnectionId {0}", connectionId);

            if (proofProposal == null)
            {
                throw new ArgumentNullException(nameof(proofProposal), "You must provide a presentation preview");
            }
            if (connectionId != null)
            {
                ConnectionRecord connection = await ConnectionService.GetAsync(agentContext, connectionId);

                if (connection.State != ConnectionState.Connected)
                {
                    throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                        $"Connection state was invalid. Expected '{ConnectionState.Connected}', found '{connection.State}'");
                }
            }
            CheckProofProposalParameters(proofProposal);


            string threadId = Guid.NewGuid().ToString();
            ProofRecord proofRecord = new()
            {
                Id = Guid.NewGuid().ToString(),
                ConnectionId = connectionId,
                ProposalJson = proofProposal.ToJson(),
                State = ProofState.Proposed
            };

            proofRecord.SetTag(TagConstants.Role, TagConstants.Holder);
            proofRecord.SetTag(TagConstants.LastThreadId, threadId);

            await RecordService.AddAsync(agentContext.AriesStorage, proofRecord);

            ProposePresentationMessage message = new(agentContext.UseMessageTypesHttps)
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

            ProofProposal proofProposal = new()
            {
                Comment = proposePresentationMessage.Comment,
                ProposedAttributes = proposePresentationMessage.PresentationPreviewMessage.ProposedAttributes.ToList<ProposedAttribute>(),
                ProposedPredicates = proposePresentationMessage.PresentationPreviewMessage.ProposedPredicates.ToList<ProposedPredicate>()
            };

            ProofRecord proofRecord = new()
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
        public async Task<(RequestPresentationMessage, ProofRecord)> CreateRequestFromProposalAsync(IAgentContext agentContext, ProofRequestParameters requestParameters,
            string proofRecordId, string connectionId)
        {
            Logger.LogInformation(LoggingEvents.CreateProofRequest, "ConnectionId {0}", connectionId);

            if (proofRecordId == null)
            {
                throw new ArgumentNullException(nameof(proofRecordId), "You must provide proof record Id");
            }
            if (connectionId != null)
            {
                ConnectionRecord connection = await ConnectionService.GetAsync(agentContext, connectionId);

                if (connection.State != ConnectionState.Connected)
                {
                    throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                        $"Connection state was invalid. Expected '{ConnectionState.Connected}', found '{connection.State}'");
                }
            }

            ProofRecord proofRecord = await RecordService.GetAsync<ProofRecord>(agentContext.AriesStorage, proofRecordId);
            ProofProposal proofProposal = proofRecord.ProposalJson.ToObject<ProofProposal>();


            // Build Proof Request from Proposal info
            ProofRequest proofRequest = new()
            {
                Name = requestParameters.Name,
                Version = requestParameters.Version,
                Nonce = await IndySharedRsPresReq.GenerateNonceAsync(),
                RequestedAttributes = new Dictionary<string, ProofAttributeInfo>(),
                NonRevoked = requestParameters.NonRevoked
            };

            Dictionary<string, List<ProposedAttribute>> attributesByReferent = new();
            foreach (ProposedAttribute proposedAttribute in proofProposal.ProposedAttributes)
            {
                proposedAttribute.Referent ??= Guid.NewGuid().ToString();

                if (attributesByReferent.TryGetValue(proposedAttribute.Referent, out List<ProposedAttribute> referentAttributes))
                {
                    referentAttributes.Add(proposedAttribute);
                }
                else
                {
                    attributesByReferent.Add(proposedAttribute.Referent, new List<ProposedAttribute> { proposedAttribute });
                }
            }

            foreach (KeyValuePair<string, List<ProposedAttribute>> referent in attributesByReferent.AsEnumerable())
            {
                List<ProposedAttribute> proposedAttributes = referent.Value;
                string attributeName = proposedAttributes.Count == 1 ? proposedAttributes.Single().Name : null;
                string[] attributeNames = proposedAttributes.Count > 1 ? proposedAttributes.ConvertAll<string>(r => r.Name).ToArray() : null;


                ProofAttributeInfo requestedAttribute = new()
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
                Console.WriteLine($"Added Attribute to Proof Request \n {proofRequest}");
            }

            foreach (ProposedPredicate pred in proofProposal.ProposedPredicates)
            {
                pred.Referent ??= Guid.NewGuid().ToString();
                ProofPredicateInfo predicate = new()
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

            RequestPresentationMessage message = new(agentContext.UseMessageTypesHttps)
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
            string connectionId)
        {
            return CreateRequestAsync(
                agentContext: agentContext,
                proofRequestJson: proofRequest?.ToJson(),
                connectionId: connectionId);
        }

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
                ConnectionRecord connection = await ConnectionService.GetAsync(agentContext, connectionId);

                if (connection.State != ConnectionState.Connected)
                {
                    throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                        $"Connection state was invalid. Expected '{ConnectionState.Connected}', found '{connection.State}'");
                }
            }

            string threadId = Guid.NewGuid().ToString();
            ProofRecord proofRecord = new()
            {
                Id = Guid.NewGuid().ToString(),
                ConnectionId = connectionId,
                RequestJson = proofRequestJson
            };
            proofRecord.SetTag(TagConstants.Role, TagConstants.Requestor);
            proofRecord.SetTag(TagConstants.LastThreadId, threadId);
            await RecordService.AddAsync(agentContext.AriesStorage, proofRecord);

            RequestPresentationMessage message = new(agentContext.UseMessageTypesHttps)
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
            (RequestPresentationMessage message, ProofRecord record) = await CreateRequestAsync(agentContext, proofRequest, null);
            ProvisioningRecord provisioning = await ProvisioningService.GetProvisioningAsync(agentContext.AriesStorage);

            message.AddDecorator(provisioning.ToServiceDecorator(useDidKeyFormat), DecoratorNames.ServiceDecorator);
            record.SetTag("RequestData", message.ToByteArray().ToBase64UrlString());

            return (message, record);
        }

        /// <inheritdoc />
        public virtual async Task<ProofRecord> ProcessRequestAsync(IAgentContext agentContext, RequestPresentationMessage requestPresentationMessage, ConnectionRecord connection)
        {
            Attachment requestAttachment = requestPresentationMessage.Requests.FirstOrDefault(x => x.Id == "libindy-request-presentation-0")
                ?? throw new ArgumentException("Presentation request attachment not found.");

            string requestJson = requestAttachment.Data.Base64.GetBytesFromBase64().GetUTF8String();

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
            ProofRecord proofRecord = await this.GetByThreadIdAsync(agentContext, presentationMessage.GetThreadId());

            Attachment requestAttachment = presentationMessage.Presentations.FirstOrDefault(x => x.Id == "libindy-presentation-0")
                ?? throw new ArgumentException("Presentation attachment not found.");

            string proofJson = requestAttachment.Data.Base64.GetBytesFromBase64().GetUTF8String();

            if (proofRecord.State != ProofState.Requested)
            {
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Proof state was invalid. Expected '{ProofState.Requested}', found '{proofRecord.State}'");
            }

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
        public virtual Task<string> CreatePresentationAsync(IAgentContext agentContext, ProofRequest proofRequest, RequestedCredentials requestedCredentials)
        {
            return CreateProofAsync(agentContext, proofRequest, requestedCredentials);
        }

        /// <inheritdoc />
        public virtual async Task<(PresentationMessage, ProofRecord)> CreatePresentationAsync(IAgentContext agentContext, string proofRecordId, RequestedCredentials requestedCredentials)
        {
            ProofRecord record = await GetAsync(agentContext, proofRecordId);

            if (record.State != ProofState.Requested)
            {
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Proof state was invalid. Expected '{ProofState.Requested}', found '{record.State}'");
            }

            string proofJson = await CreatePresentationAsync(
                agentContext,
                record.RequestJson.ToObject<ProofRequest>(),
                requestedCredentials);

            record.ProofJson = proofJson;
            await record.TriggerAsync(ProofTrigger.Accept);
            await RecordService.UpdateAsync(agentContext.AriesStorage, record);

            string threadId = record.GetTag(TagConstants.LastThreadId);

            PresentationMessage proofMsg = new(agentContext.UseMessageTypesHttps)
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

        private async Task<List<string>> BuildSchemasAsync(IAgentContext agentContext, IEnumerable<string> schemaIds)
        {
            List<string> result = new();

            foreach (string schemaId in schemaIds)
            {
                AriesResponse ledgerSchema = await LedgerService.LookupSchemaAsync(agentContext, schemaId);
                result.Add(ledgerSchema.ObjectJson);
            }

            return result;
        }

        private async Task<List<string>> BuildCredentialDefinitionsAsync(IAgentContext agentContext, IEnumerable<string> credentialDefIds)
        {
            List<string> result = new();

            foreach (string credDefId in credentialDefIds)
            {
                AriesResponse ledgerDefinition = await LedgerService.LookupDefinitionAsync(agentContext, credDefId);
                result.Add(ledgerDefinition.ObjectJson);
            }

            return result;
        }

        private bool HasNonRevokedOnAttributeLevel(ProofRequest proofRequest)
        {
            foreach (KeyValuePair<string, ProofAttributeInfo> proofRequestRequestedAttribute in proofRequest.RequestedAttributes)
            {
                if (proofRequestRequestedAttribute.Value.NonRevoked != null)
                {
                    return true;
                }
            }

            foreach (KeyValuePair<string, ProofPredicateInfo> proofRequestRequestedPredicate in proofRequest.RequestedPredicates)
            {
                if (proofRequestRequestedPredicate.Value.NonRevoked != null)
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<(AriesRegistryResponse, string)> BuildRevocationStateAsync(
            IAgentContext agentContext, CredentialInfo credentialInfo, AriesResponse registryDefinition,
            RevocationInterval nonRevoked)
        {
            AriesRegistryResponse delta = await LedgerService.LookupRevocationRegistryDeltaAsync(
                agentContext: agentContext,
                revocationRegistryId: credentialInfo.RevocationRegistryId,
                // Ledger will not return correct revocation state if the 'from' field
                // is other than 0
                from: 0, //nonRevoked.From,
                to: nonRevoked.To);

            RevocationRegistryRecord revRegRecord = await RecordService.GetAsync<RevocationRegistryRecord>(agentContext.AriesStorage, credentialInfo.RevocationRegistryId);

            string state = await IndySharedRsRev.CreateOrUpdateRevocationStateAsync(
                registryDefinition.ObjectJson,
                delta.ObjectJson,
                0,
                (long)delta.Timestamp,
                revRegRecord.TailsLocation,
                new CredentialRevocationState().JsonString);

            return (delta, state);
        }

        private async Task<string> BuildRevocationStatesAsync(IAgentContext agentContext,
            IEnumerable<CredentialInfo> credentialObjects,
            ProofRequest proofRequest,
            RequestedCredentials requestedCredentials)
        {
            List<RequestedAttribute> allCredentials = new();
            allCredentials.AddRange(requestedCredentials.RequestedAttributes.Values);
            allCredentials.AddRange(requestedCredentials.RequestedPredicates.Values);

            Dictionary<string, Dictionary<string, JObject>> result = new();

            if (proofRequest.NonRevoked == null && !HasNonRevokedOnAttributeLevel(proofRequest))
            {
                return result.ToJson();
            }

            foreach (RequestedAttribute requestedCredential in allCredentials)
            {
                // ReSharper disable once PossibleMultipleEnumeration
                CredentialInfo credential = credentialObjects.First(x => x.Referent == requestedCredential.CredentialId);
                if (credential.RevocationRegistryId == null)
                {
                    continue;
                }

                AriesResponse registryDefinition = await LedgerService.LookupRevocationRegistryDefinitionAsync(
                    agentContext: agentContext,
                    registryId: credential.RevocationRegistryId);

                if (proofRequest.NonRevoked != null)
                {
                    (AriesRegistryResponse delta, string state) = await BuildRevocationStateAsync(
                        agentContext, credential, registryDefinition, proofRequest.NonRevoked);

                    if (!result.ContainsKey(credential.RevocationRegistryId))
                    {
                        result.Add(credential.RevocationRegistryId, new Dictionary<string, JObject>());
                    }

                    requestedCredential.Timestamp = (long)delta.Timestamp;
                    if (!result[credential.RevocationRegistryId].ContainsKey($"{delta.Timestamp}"))
                    {
                        result[credential.RevocationRegistryId].Add($"{delta.Timestamp}", JObject.Parse(state));
                    }

                    continue;
                }

                foreach (KeyValuePair<string, ProofAttributeInfo> proofRequestRequestedAttribute in proofRequest.RequestedAttributes)
                {
                    RevocationInterval revocationInterval = proofRequestRequestedAttribute.Value.NonRevoked;
                    (AriesRegistryResponse delta, string state) = await BuildRevocationStateAsync(
                        agentContext, credential, registryDefinition, revocationInterval);

                    if (!result.ContainsKey(credential.RevocationRegistryId))
                    {
                        result.Add(credential.RevocationRegistryId, new Dictionary<string, JObject>());
                    }

                    requestedCredential.Timestamp = (long)delta.Timestamp;
                    if (!result[credential.RevocationRegistryId].ContainsKey($"{delta.Timestamp}"))
                    {
                        result[credential.RevocationRegistryId].Add($"{delta.Timestamp}", JObject.Parse(state));
                    }
                }

                foreach (KeyValuePair<string, ProofPredicateInfo> proofRequestRequestedPredicate in proofRequest.RequestedPredicates)
                {
                    RevocationInterval revocationInterval = proofRequestRequestedPredicate.Value.NonRevoked;
                    (AriesRegistryResponse delta, string state) = await BuildRevocationStateAsync(
                        agentContext, credential, registryDefinition, revocationInterval);

                    if (!result.ContainsKey(credential.RevocationRegistryId))
                    {
                        result.Add(credential.RevocationRegistryId, new Dictionary<string, JObject>());
                    }

                    requestedCredential.Timestamp = (long)delta.Timestamp;
                    if (!result[credential.RevocationRegistryId].ContainsKey($"{delta.Timestamp}"))
                    {
                        result[credential.RevocationRegistryId].Add($"{delta.Timestamp}", JObject.Parse(state));
                    }
                }
            }

            return result.ToJson();
        }

        private async Task<List<string>> BuildRevocationRegistriesAsync(
            IAgentContext agentContext,
            IEnumerable<ProofIdentifier> proofIdentifiers)
        {
            List<string> result = new();

            foreach (ProofIdentifier identifier in proofIdentifiers)
            {
                if (identifier.Timestamp == null)
                {
                    continue;
                }

                AriesRegistryResponse revocationRegistry = await LedgerService.LookupRevocationRegistryAsync(
                    agentContext,
                    identifier.RevocationRegistryId,
                    long.Parse(identifier.Timestamp));

                result.Add(revocationRegistry.ObjectJson);
            }

            return result;
        }

        private async Task<List<string>> BuildRevocationRegistryDefinitionsAsync(IAgentContext agentContext,
            IEnumerable<string> revocationRegistryIds)
        {
            List<string> result = new();

            foreach (string revRegId in revocationRegistryIds)
            {
                AriesResponse ledgerRegistry = await LedgerService.LookupRevocationRegistryDefinitionAsync(agentContext, revRegId);
                result.Add(ledgerRegistry.ObjectJson);
            }

            return result;
        }

        private void CheckProofProposalParameters(ProofProposal proofProposal)
        {
            if (proofProposal.ProposedAttributes.Count > 1)
            {
                List<ProposedAttribute> attrList = proofProposal.ProposedAttributes;
                Dictionary<string, ProposedAttribute> referents = new();

                // Check if all attributes that share referent have same requirements
                for (int i = 0; i < attrList.Count; i++)
                {
                    ProposedAttribute attr = attrList[i];
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
                List<ProposedPredicate> predList = proofProposal.ProposedPredicates;
                Dictionary<string, ProposedPredicate> referents = new();

                for (int i = 0; i < predList.Count; i++)
                {
                    ProposedPredicate pred = predList[i];
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

        private async Task<List<IssueCredential.Credential>> ProverSearchCredentialsForProofRequestAsync(IAgentContext agentContext,
            ProofRequest proofRequest)
        {
            List<IssueCredential.Credential> result = new();

            List<ISearchQuery> queryList = new();

            foreach (ProofAttributeInfo attributeInfo in proofRequest.RequestedAttributes.Select(x => x.Value))
            {
                foreach (AttributeFilter attributeFilter in attributeInfo.Restrictions)
                {
                    List<ISearchQuery> currentQueryList = new()
                    {
                        SearchQuery.Equal("State", "Issued")
                    };

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

            foreach (ProofPredicateInfo predicateInfo in proofRequest.RequestedPredicates.Select(x => x.Value))
            {
                foreach (AttributeFilter attributeFilter in predicateInfo.Restrictions)
                {
                    List<ISearchQuery> currentQueryList = new()
                    {
                        SearchQuery.Equal("State", "Issued")
                    };

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

            ISearchQuery finalQuery = SearchQuery.Or(queryList.ToArray());

            List<CredentialRecord> credRecs = await RecordService.SearchAsync<CredentialRecord>(agentContext.AriesStorage, finalQuery, count: 2147483647);
            result.AddRange(from CredentialRecord cred in credRecs
                            where !string.IsNullOrEmpty(cred.CredentialJson)
                            select JsonConvert.DeserializeObject<IssueCredential.Credential>(cred.CredentialJson));
            return result;
        }

        /*private async Task<string> GetRecordJson(IAgentContext agentContext, string recordId)
        {
            Store wallet = agentContext.WalletStore;
            if (wallet.session == null)
            {
                _ = await AriesAskarStore.StartSessionAsync(wallet);
            }

            try
            {
                IntPtr recordHandle = await AriesAskarStore.FetchAsync(
                    wallet.session,
                    "AF.CredentialRecord",
                    recordId);
                if (recordHandle == new IntPtr())
                    return null;

                List<string> records = new();
                SearchOptions options = new();
                int numRecords = await AriesAskarResults.EntryListCountAsync(recordHandle);
                for (int i = 0; i < numRecords; i++)
                {
                    string type = options.RetrieveType ? await AriesAskarResults.EntryListGetCategoryAsync(recordHandle, i) : null;
                    string value = options.RetrieveValue ? await AriesAskarResults.EntryListGetValueAsync(recordHandle, i) : null;
                    string tags = options.RetrieveTags ? await AriesAskarResults.EntryListGetTagsAsync(recordHandle, i) : null;
                    records.Add(JsonConvert.SerializeObject(new
                    {
                        id = await AriesAskarResults.EntryListGetNameAsync(recordHandle, i),
                        type,
                        value,
                        tags,
                    }));
                }

                JsonSerializerSettings jsonSettings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Converters = new List<JsonConverter>
                    {
                        new AgentEndpointJsonConverter(),
                        new AttributeFilterConverter()
                    }
                };
                SearchItem item = JsonConvert.DeserializeObject<SearchItem>(records.First(), jsonSettings);

                return item.Value;
            }
            catch (WalletItemNotFoundException)
            {
                return null;
            }
        }*/
        #endregion
    }
}
