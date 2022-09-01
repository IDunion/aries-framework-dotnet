using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Decorators.Attachments;
using Hyperledger.Aries.Decorators.Signature;
using Hyperledger.Aries.Decorators.Threading;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.Handshakes.Common;
using Hyperledger.Aries.Features.Handshakes.Common.Dids;
using Hyperledger.Aries.Features.Handshakes.Connection.Extensions;
using Hyperledger.Aries.Features.Handshakes.Connection.Models;
using Hyperledger.Aries.Features.OutOfBand;
using Hyperledger.Aries.Models.Events;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Utils;
using Hyperledger.Indy.CryptoApi;
using Hyperledger.Indy.DidApi;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Features.Handshakes.Connection
{
    /// <inheritdoc />
    public class DefaultConnectionService : IConnectionService
    {
        /// <summary>
        /// The event aggregator.
        /// </summary>
        protected readonly IEventAggregator EventAggregator;
        /// <summary>
        /// The record service
        /// </summary>
        protected readonly IWalletRecordService RecordService;
        /// <summary>
        /// The provisioning service
        /// </summary>
        protected readonly IProvisioningService ProvisioningService;
        /// <summary>
        /// The logger
        /// </summary>
        protected readonly ILogger<DefaultConnectionService> Logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultConnectionService"/> class.
        /// </summary>
        /// <param name="eventAggregator">The event aggregator.</param>
        /// <param name="recordService">The record service.</param>
        /// <param name="provisioningService">The provisioning service.</param>
        /// <param name="logger">The logger.</param>
        public DefaultConnectionService(
            IEventAggregator eventAggregator,
            IWalletRecordService recordService,
            IProvisioningService provisioningService,
            ILogger<DefaultConnectionService> logger)
        {
            EventAggregator = eventAggregator;
            ProvisioningService = provisioningService;
            Logger = logger;
            RecordService = recordService;
        }

        /// <inheritdoc />
        public virtual async Task<(ConnectionInvitationMessage, ConnectionRecord)> CreateInvitationAsync(IAgentContext agentContext,
            InviteConfiguration config = null)
        {
            if (agentContext.AriesStorage.Wallet is null)
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Indy.WalletApi.Wallet)} which must not be null.");
            }

            config ??= new InviteConfiguration();
            ConnectionRecord connection = new() { Role = ConnectionRole.Inviter };
            connection.Id = config.ConnectionId ?? connection.Id;

            Logger.LogInformation(LoggingEvents.CreateInvitation, "ConnectionId {0}", connection.Id);

            string connectionKey = await Crypto.CreateKeyAsync(agentContext.AriesStorage.Wallet, "{}");
            connection.SetTag(TagConstants.ConnectionKey, connectionKey);

            if (config.AutoAcceptConnection)
            {
                connection.SetTag(TagConstants.AutoAcceptConnection, "true");
            }

            connection.MultiPartyInvitation = config.MultiPartyInvitation;

            if (!config.MultiPartyInvitation)
            {
                connection.Alias = config.TheirAlias;
                if (!string.IsNullOrEmpty(config.TheirAlias.Name))
                {
                    connection.SetTag(TagConstants.Alias, config.TheirAlias.Name);
                }
            }

            foreach (KeyValuePair<string, string> tag in config.Tags)
            {
                connection.SetTag(tag.Key, tag.Value);
            }

            ProvisioningRecord provisioning = await ProvisioningService.GetProvisioningAsync(agentContext.AriesStorage);

            if (string.IsNullOrEmpty(provisioning.Endpoint.Uri))
            {
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState, "Provision record has no endpoint information specified");
            }

            await RecordService.AddAsync(agentContext.AriesStorage, connection);

            IList<string> routingKeys = null;
            if (provisioning.Endpoint.Verkey != null)
            {
                routingKeys = (config.UseDidKeyFormat
                    ? provisioning.Endpoint.Verkey
                        .Where(DidUtils.IsFullVerkey)
                        .Select(DidUtils.ConvertVerkeyToDidKey)
                    : provisioning.Endpoint.Verkey).ToList();
            }
            string recipientKey = config.UseDidKeyFormat ? DidUtils.ConvertVerkeyToDidKey(connectionKey) : connectionKey;

            return (new ConnectionInvitationMessage(agentContext.UseMessageTypesHttps)
            {
                ServiceEndpoint = provisioning.Endpoint.Uri,
                RoutingKeys = routingKeys,
                RecipientKeys = new[] { recipientKey },
                Label = config.MyAlias.Name ?? provisioning.Owner.Name,
                ImageUrl = config.MyAlias.ImageUrl ?? provisioning.Owner.ImageUrl
            }, connection);
        }

        /// <inheritdoc />
        public async Task RevokeInvitationAsync(IAgentContext agentContext, string invitationId)
        {
            ConnectionRecord connection = await GetAsync(agentContext, invitationId);

            if (connection.State != ConnectionState.Invited)
            {
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Connection state was invalid. Expected '{ConnectionState.Invited}', found '{connection.State}'");
            }

            if (agentContext.AriesStorage.Wallet is null)
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Indy.WalletApi.Wallet)} which must not be null.");
            }

            _ = await RecordService.DeleteAsync<ConnectionRecord>(agentContext.AriesStorage, invitationId);
        }

        /// <inheritdoc />
        public async Task<ConnectionRecord> ProcessInvitationAsync(IAgentContext agentContext, ConnectionInvitationMessage invitation)
        {
            if (agentContext.AriesStorage.Wallet is null)
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Indy.WalletApi.Wallet)} which must not be null.");
            }

            CreateAndStoreMyDidResult my = await Did.CreateAndStoreMyDidAsync(agentContext.AriesStorage.Wallet, "{}");

            ConnectionRecord connection = new()
            {
                Endpoint = new AgentEndpoint(invitation.ServiceEndpoint, null, invitation.RoutingKeys != null && invitation.RoutingKeys.Count != 0 ? invitation.RoutingKeys.ToArray() : null),
                MyDid = my.Did,
                MyVk = my.VerKey,
                Role = ConnectionRole.Invitee
            };
            connection.SetTag(TagConstants.InvitationKey, invitation.RecipientKeys.First());

            if (!string.IsNullOrEmpty(invitation.Label) || !string.IsNullOrEmpty(invitation.ImageUrl))
            {
                connection.Alias = new ConnectionAlias
                {
                    Name = invitation.Label,
                    ImageUrl = invitation.ImageUrl
                };

                if (string.IsNullOrEmpty(invitation.Label))
                {
                    connection.SetTag(TagConstants.Alias, invitation.Label);
                }
            }

            await RecordService.AddAsync(agentContext.AriesStorage, connection);

            EventAggregator.Publish(new ServiceMessageProcessingEvent
            {
                MessageType = invitation.Type,
                RecordId = connection.Id,
                ThreadId = invitation.GetThreadId()
            });

            return connection;
        }

        /// <inheritdoc />
        public virtual async Task<ConnectionRecord> ProcessInvitationAsync(IAgentContext agentContext, InvitationMessage invitation)
        {
            // Todo: Check for existing ConnectionRecords
            // Based on recipient key only

            DidCommServiceEndpoint service = null;
            foreach (object obj in invitation.Services)
            {
                if (obj is DidCommServiceEndpoint serviceEndpoint)
                {
                    service = serviceEndpoint;
                    break;
                }
            }

            if (service == null)
            {
                throw new ArgumentNullException(nameof(invitation.Services), "No service endpoint defined");
            }

            CreateAndStoreMyDidResult my = await Did.CreateAndStoreMyDidAsync(agentContext.AriesStorage.Wallet, "{}");

            ConnectionRecord connection = new()
            {
                Endpoint = new AgentEndpoint(service.ServiceEndpoint, null, service.RoutingKeys != null && service.RoutingKeys.Count != 0 ? service.RoutingKeys.ToArray() : null),
                MyDid = my.Did,
                MyVk = my.VerKey,
                Role = ConnectionRole.Invitee
            };
            connection.SetTag(TagConstants.InvitationKey, service.RecipientKeys.First());

            if (!string.IsNullOrEmpty(invitation.Label))
            {
                connection.Alias = new ConnectionAlias
                {
                    Name = invitation.Label
                };

                if (string.IsNullOrEmpty(invitation.Label))
                {
                    connection.SetTag(TagConstants.Alias, invitation.Label);
                }
            }
            await RecordService.AddAsync(agentContext.AriesStorage, connection);

            return connection;
        }

        /// <inheritdoc />
        public async Task<(ConnectionRequestMessage, ConnectionRecord)> CreateRequestAsync(IAgentContext agentContext, ConnectionRecord connection)
        {
            Logger.LogInformation(LoggingEvents.AcceptInvitation, "Key {0}, Endpoint {1}",
                connection.Endpoint.Verkey, connection.Endpoint.Uri);

            await connection.TriggerAsync(ConnectionTrigger.Request);

            ProvisioningRecord provisioning = await ProvisioningService.GetProvisioningAsync(agentContext.AriesStorage);
            ConnectionRequestMessage request = new(agentContext.UseMessageTypesHttps)
            {
                Connection = new Common.Connection
                {
                    Did = connection.MyDid,
                    DidDoc = connection.MyDidDoc(provisioning)
                },
                Label = provisioning.Owner?.Name,
                ImageUrl = provisioning.Owner?.ImageUrl
            };

            // also set image as attachment
            if (provisioning.Owner?.ImageUrl != null)
            {
                request.AddAttachment(new Attachment
                {
                    Nickname = "profile-image",
                    Data = new AttachmentContent { Links = new[] { provisioning.Owner.ImageUrl } }
                });
            }

            await RecordService.UpdateAsync(agentContext.AriesStorage, connection);

            return (request, connection);
        }

        /// <inheritdoc />
        public virtual async Task<(ConnectionRequestMessage, ConnectionRecord)> CreateRequestAsync(IAgentContext agentContext, ConnectionInvitationMessage invitation)
        {
            ConnectionRecord connection = await ProcessInvitationAsync(agentContext, invitation);

            return await CreateRequestAsync(agentContext, connection);
        }

        /// <inheritdoc />
        public virtual async Task<string> ProcessRequestAsync(IAgentContext agentContext, ConnectionRequestMessage request, ConnectionRecord connection)
        {
            if (agentContext.AriesStorage.Wallet is null)
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Indy.WalletApi.Wallet)} which must not be null.");
            }

            Logger.LogInformation(LoggingEvents.ProcessConnectionRequest, "Did {0}", request.Connection.Did);

            CreateAndStoreMyDidResult my = await Did.CreateAndStoreMyDidAsync(agentContext.AriesStorage.Wallet, "{}");

            //TODO throw exception or a problem report if the connection request features a did doc that has no indy agent did doc convention featured
            //i.e there is no way for this agent to respond to messages. And or no keys specified
            await Did.StoreTheirDidAsync(agentContext.AriesStorage.Wallet, new { did = request.Connection.Did, verkey = request.Connection.DidDoc.Keys[0].PublicKeyBase58 }.ToJson());

            if (request.Connection.DidDoc.Services != null &&
                request.Connection.DidDoc.Services.Count > 0 &&
                request.Connection.DidDoc.Services[0] is IndyAgentDidDocService service)
            {
                connection.Endpoint = new AgentEndpoint(service.ServiceEndpoint, null, service.RoutingKeys != null && service.RoutingKeys.Count > 0 ? service.RoutingKeys.ToArray() : null);
            }

            connection.TheirDid = request.Connection.Did;
            connection.TheirVk = request.Connection.DidDoc.Keys[0].PublicKeyBase58;
            connection.MyDid = my.Did;
            connection.MyVk = my.VerKey;

            connection.SetTag(TagConstants.LastThreadId, request.Id);

            if (connection.Alias == null)
            {
                connection.Alias = new ConnectionAlias();
            }

            if (!string.IsNullOrEmpty(request.Label) && string.IsNullOrEmpty(connection.Alias.Name))
            {
                connection.Alias.Name = request.Label;
            }

            if (!string.IsNullOrEmpty(request.ImageUrl) && string.IsNullOrEmpty(connection.Alias.ImageUrl))
            {
                connection.Alias.ImageUrl = request.ImageUrl;
            }

            if (!connection.MultiPartyInvitation)
            {
                await connection.TriggerAsync(ConnectionTrigger.Request);
                await RecordService.UpdateAsync(agentContext.AriesStorage, connection);

                EventAggregator.Publish(new ServiceMessageProcessingEvent
                {
                    RecordId = connection.Id,
                    MessageType = request.Type,
                    ThreadId = request.GetThreadId()
                });

                return connection.Id;
            }
            else
            {
                ConnectionRecord newConnection = connection.DeepCopy();
                newConnection.Id = Guid.NewGuid().ToString();
                newConnection.MultiPartyInvitation = false;

                await newConnection.TriggerAsync(ConnectionTrigger.Request);
                await RecordService.AddAsync(agentContext.AriesStorage, newConnection);

                EventAggregator.Publish(new ServiceMessageProcessingEvent
                {
                    RecordId = newConnection.Id,
                    MessageType = request.Type,
                    ThreadId = request.GetThreadId()
                });
                return newConnection.Id;
            }
        }

        /// <inheritdoc />
        public virtual async Task<string> ProcessResponseAsync(IAgentContext agentContext, ConnectionResponseMessage response, ConnectionRecord connection)
        {
            Logger.LogTrace(LoggingEvents.AcceptConnectionResponse, "To {1}", connection.MyDid);

            await connection.TriggerAsync(ConnectionTrigger.Response);

            //TODO throw exception or a problem report if the connection request features a did doc that has no indy agent did doc convention featured
            //i.e there is no way for this agent to respond to messages. And or no keys specified
            Common.Connection connectionObj = await SignatureUtils.UnpackAndVerifyAsync<Common.Connection>(response.ConnectionSig, agentContext);

            await Did.StoreTheirDidAsync(agentContext.AriesStorage.Wallet,
                new { did = connectionObj.Did, verkey = connectionObj.DidDoc.Keys[0].PublicKeyBase58 }.ToJson());

            connection.TheirDid = connectionObj.Did;
            connection.TheirVk = connectionObj.DidDoc.Keys[0].PublicKeyBase58;

            connection.SetTag(TagConstants.LastThreadId, response.GetThreadId());

            if (connectionObj.DidDoc.Services[0] is IndyAgentDidDocService service)
            {
                connection.Endpoint = new AgentEndpoint(service.ServiceEndpoint, null, service.RoutingKeys != null && service.RoutingKeys.Count > 0 ? service.RoutingKeys.ToArray() : null);
            }

            await RecordService.UpdateAsync(agentContext.AriesStorage, connection);
            EventAggregator.Publish(new ServiceMessageProcessingEvent
            {
                RecordId = connection.Id,
                MessageType = response.Type,
                ThreadId = response.GetThreadId()
            });

            return connection.Id;
        }

        /// <inheritdoc />
        public virtual async Task<(ConnectionResponseMessage, ConnectionRecord)> CreateResponseAsync(IAgentContext agentContext, string connectionId)
        {
            Logger.LogTrace(LoggingEvents.AcceptConnectionRequest, "ConnectionId {0}", connectionId);

            ConnectionRecord connection = await GetAsync(agentContext, connectionId);

            if (connection.State != ConnectionState.Negotiating)
            {
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState,
                    $"Connection state was invalid. Expected '{ConnectionState.Negotiating}', found '{connection.State}'");
            }

            await connection.TriggerAsync(ConnectionTrigger.Response);
            await RecordService.UpdateAsync(agentContext.AriesStorage, connection);

            // Send back response message
            ProvisioningRecord provisioning = await ProvisioningService.GetProvisioningAsync(agentContext.AriesStorage);

            Common.Connection connectionData = new()
            {
                Did = connection.MyDid,
                DidDoc = connection.MyDidDoc(provisioning)
            };

            SignatureDecorator sigData = await SignatureUtils.SignDataAsync(agentContext, RecordService, connectionData, connection.GetTag(TagConstants.ConnectionKey));
            string threadId = connection.GetTag(TagConstants.LastThreadId);

            ConnectionResponseMessage response = new(agentContext.UseMessageTypesHttps) { ConnectionSig = sigData };
            response.ThreadFrom(threadId);

            return (response, connection);
        }

        /// <inheritdoc />
        public virtual async Task<ConnectionRecord> GetAsync(IAgentContext agentContext, string connectionId)
        {
            Logger.LogTrace(LoggingEvents.GetConnection, "ConnectionId {0}", connectionId);

            ConnectionRecord record = await RecordService.GetAsync<ConnectionRecord>(agentContext.AriesStorage, connectionId);

            return record == null ? throw new AriesFrameworkException(ErrorCode.RecordNotFound, "Connection record not found") : record;
        }

        /// <inheritdoc />
        public virtual Task<List<ConnectionRecord>> ListAsync(IAgentContext agentContext, ISearchQuery query = null,
            int count = 100, int skip = 0)
        {
            Logger.LogTrace(LoggingEvents.ListConnections, "List Connections");

            return RecordService.SearchAsync<ConnectionRecord>(agentContext.AriesStorage, query, null, count, skip);
        }

        /// <inheritdoc />
        public virtual async Task<bool> DeleteAsync(IAgentContext agentContext, string connectionId)
        {
            Logger.LogTrace(LoggingEvents.DeleteConnection, "ConnectionId {0}", connectionId);

            return await RecordService.DeleteAsync<ConnectionRecord>(agentContext.AriesStorage, connectionId);
        }

        /// <inheritdoc />
        public async Task<ConnectionAcknowledgeMessage> CreateAcknowledgementMessageAsync(IAgentContext agentContext, string connectionRecordId,
            string status = AcknowledgementStatusConstants.Ok)
        {
            ConnectionRecord record = await GetAsync(agentContext, connectionRecordId);

            string threadId = record.GetTag(TagConstants.LastThreadId);
            ConnectionAcknowledgeMessage acknowledgeMessage = new(agentContext.UseMessageTypesHttps)
            {
                Id = threadId,
                Status = status
            };
            acknowledgeMessage.ThreadFrom(threadId);

            return acknowledgeMessage;
        }

        /// <inheritdoc />
        public async Task<ConnectionRecord> ProcessAcknowledgementMessageAsync(IAgentContext agentContext,
            ConnectionAcknowledgeMessage acknowledgeMessage)
        {
            ConnectionRecord connectionRecord = await this.GetByThreadIdAsync(agentContext, acknowledgeMessage.GetThreadId());

            EventAggregator.Publish(new ServiceMessageProcessingEvent
            {
                RecordId = connectionRecord.Id,
                MessageType = acknowledgeMessage.Type,
                ThreadId = acknowledgeMessage.GetThreadId()
            });

            return connectionRecord;
        }

        public virtual async Task<ConnectionRecord> ResolveByMyKeyAsync(IAgentContext agentContext, string myKey)
        {
            if (string.IsNullOrEmpty(myKey))
            {
                throw new ArgumentNullException(nameof(myKey));
            }

            if (agentContext == null)
            {
                throw new ArgumentNullException(nameof(agentContext));
            }

            ConnectionRecord record =
                // Check if key is part of a connection
                (await ListAsync(agentContext,
                    SearchQuery.Equal(nameof(ConnectionRecord.MyVk), myKey), 5))
                .SingleOrDefault()

                // Check if key is part of a multiparty invitation
                ?? (await ListAsync(agentContext,
                    SearchQuery.And(
                        SearchQuery.Equal(TagConstants.ConnectionKey, myKey),
                        SearchQuery.Equal(nameof(ConnectionRecord.MultiPartyInvitation), "True")), 5))
                .SingleOrDefault()

                // Check if key is part of a single party invitation
                ?? (await ListAsync(agentContext,
                    SearchQuery.Equal(TagConstants.ConnectionKey, myKey), 5))
                .SingleOrDefault();

            return record;
        }
    }
}
