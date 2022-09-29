﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Decorators.Transport;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Aries.Utils;
using Microsoft.Extensions.Logging;

namespace Hyperledger.Aries.Agents
{
    /// <inheritdoc />
    public class DefaultMessageService : IMessageService
    {
        /// <summary>The agent wire message MIME type</summary>
        public const string AgentWireMessageMimeType = "application/ssi-agent-wire";

        /// <summary>The logger</summary>
        // ReSharper disable InconsistentNaming
        protected readonly ILogger<DefaultMessageService> Logger;

        /// <summary>The HTTP client</summary>
        protected readonly IEnumerable<IMessageDispatcher> MessageDispatchers;

        protected readonly IWalletRecordService RecordService;
        // ReSharper restore InconsistentNaming

        /// <summary>Initializes a new instance of the <see cref="DefaultMessageService"/> class.</summary>
        /// <param name="logger">The logger.</param>
        /// <param name="messageDispatchers">The message handler.</param>
        /// <param name="recordService">The record service.</param>
        public DefaultMessageService(
            ILogger<DefaultMessageService> logger,
            IEnumerable<IMessageDispatcher> messageDispatchers,
            IWalletRecordService recordService)
        {
            Logger = logger;
            MessageDispatchers = messageDispatchers;
            RecordService = recordService;
        }

        private async Task<UnpackedMessageContext> UnpackAsync(AriesStorage storage, PackedMessageContext message, string senderKey, IWalletRecordService recordService)
        {
            UnpackResult unpacked;

            try
            {
                unpacked = await CryptoUtils.UnpackAsync(storage, message.Payload, recordService);
            }
            catch (Exception e)
            {
                Logger.LogError("Failed to un-pack message", e);
                throw new AriesFrameworkException(ErrorCode.InvalidMessage, "Failed to un-pack message", e);
            }
            return new UnpackedMessageContext(unpacked.Message, senderKey);
        }

        /// <inheritdoc />
        public virtual async Task SendAsync(IAgentContext agentContext, AgentMessage message, string recipientKey,
            string endpointUri, string[] routingKeys = null, string senderKey = null)
        {
            Logger.LogInformation(LoggingEvents.SendMessage, "Recipient {0} Endpoint {1}", recipientKey,
                endpointUri);

            if (string.IsNullOrEmpty(message.Id))
                throw new AriesFrameworkException(ErrorCode.InvalidMessage, "@id field on message must be populated");

            if (string.IsNullOrEmpty(message.Type))
                throw new AriesFrameworkException(ErrorCode.InvalidMessage, "@type field on message must be populated");

            if (string.IsNullOrEmpty(endpointUri))
                throw new ArgumentNullException(nameof(endpointUri));

            var uri = new Uri(endpointUri);

            var dispatcher = GetDispatcher(uri.Scheme);

            if (dispatcher == null)
                throw new AriesFrameworkException(ErrorCode.A2AMessageTransmissionError, $"No registered dispatcher for transport scheme : {uri.Scheme}");

            var wireMsg = await CryptoUtils.PrepareAsync(agentContext, message, recipientKey, routingKeys, senderKey, RecordService);

            await dispatcher.DispatchAsync(uri, new PackedMessageContext(wireMsg));
        }

        /// <inheritdoc />
        public async Task<MessageContext> SendReceiveAsync(IAgentContext agentContext, AgentMessage message, string recipientKey,
            string endpointUri, string[] routingKeys = null, string senderKey = null, ReturnRouteTypes returnType = ReturnRouteTypes.all)
        {
            Logger.LogInformation(LoggingEvents.SendMessage, "Recipient {0} Endpoint {1}", recipientKey,
                endpointUri);

            if (string.IsNullOrEmpty(message.Id))
                throw new AriesFrameworkException(ErrorCode.InvalidMessage, "@id field on message must be populated");

            if (string.IsNullOrEmpty(message.Type))
                throw new AriesFrameworkException(ErrorCode.InvalidMessage, "@type field on message must be populated");

            if (string.IsNullOrEmpty(endpointUri))
                throw new ArgumentNullException(nameof(endpointUri));

            var uri = new Uri(endpointUri);

            var dispatcher = GetDispatcher(uri.Scheme);

            if (dispatcher == null)
                throw new AriesFrameworkException(ErrorCode.A2AMessageTransmissionError, $"No registered dispatcher for transport scheme : {uri.Scheme}");

            message.AddReturnRouting(returnType);
            var wireMsg = await CryptoUtils.PrepareAsync(agentContext, message, recipientKey, routingKeys, senderKey, RecordService);

            var response = await dispatcher.DispatchAsync(uri, new PackedMessageContext(wireMsg));
            if (response is PackedMessageContext responseContext)
            {
                return await UnpackAsync(agentContext.AriesStorage, responseContext, senderKey, RecordService);
            }
            throw new InvalidOperationException("Invalid or empty response");
        }

        private IMessageDispatcher GetDispatcher(string scheme) => MessageDispatchers.FirstOrDefault(_ => _.TransportSchemes.Contains(scheme));
    }
}
