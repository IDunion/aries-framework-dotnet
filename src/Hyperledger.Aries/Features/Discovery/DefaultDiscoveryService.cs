using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Decorators.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Hyperledger.Aries.Features.Discovery
{
    /// <inheritdoc />
    public class DefaultDiscoveryService : IDiscoveryService
    {
        /// <summary>
        /// The event aggregator.
        /// </summary>
        protected readonly IEventAggregator EventAggregator;
        /// <summary>
        /// The logger.
        /// </summary>
        protected readonly ILogger<DefaultDiscoveryService> Logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultDiscoveryService"/> class.
        /// </summary>
        /// <param name="eventAggregator">The event aggregator.</param>
        /// <param name="logger">The logger.</param>
        public DefaultDiscoveryService(
            IEventAggregator eventAggregator,
            ILogger<DefaultDiscoveryService> logger)
        {
            EventAggregator = eventAggregator;
            Logger = logger;
        }

        /// <inheritdoc />
        public virtual DiscoveryQueryMessage CreateQuery(IAgentContext agentContext, string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                throw new ArgumentNullException(nameof(query));
            }

            //TODO validate this is a valid query????

            return new DiscoveryQueryMessage(agentContext.UseMessageTypesHttps)
            {
                Query = query
            };
        }

        /// <inheritdoc />
        public virtual DiscoveryDiscloseMessage CreateQueryResponse(IAgentContext agentContext, DiscoveryQueryMessage message)
        {
            if (string.IsNullOrEmpty(message.Query))
            {
                throw new ArgumentNullException(nameof(message.Query));
            }

            //TODO validate this is a valid query????

            string test = message.Query.TrimEnd('*');

            IList<MessageType> supportedMessages = new List<MessageType>();
            if (message.Query == "*")
            {
                supportedMessages = agentContext.SupportedMessages;
            }
            else
            {
                supportedMessages = message.Query.EndsWith("*")
                ? (agentContext.SupportedMessages?.Where(_ => _.MessageTypeUri.StartsWith(message.Query.TrimEnd('*'))).ToList())
                : (IList<MessageType>)(agentContext.SupportedMessages?.Where(_ => _.MessageTypeUri == message.Query).ToList());
            }

            supportedMessages ??= new List<MessageType>();

            DiscoveryDiscloseMessage disclosureMessage = message.CreateThreadedReply<DiscoveryDiscloseMessage>();
            foreach (MessageType supportedMessage in supportedMessages.GroupBy(_ => _.MessageFamilyUri).Select(g => g.First()))
            {
                disclosureMessage.Protocols.Add(new DisclosedMessageProtocol
                {
                    ProtocolId = supportedMessage.MessageFamilyUri
                });
            }

            return disclosureMessage;
        }
    }
}
