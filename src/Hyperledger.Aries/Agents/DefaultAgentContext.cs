﻿using Hyperledger.Aries.Ledger.Models;
using Hyperledger.Aries.Storage.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Hyperledger.Aries.Agents
{
    /// <inheritdoc />
    /// <summary>
    /// Default Agent Context Object.
    /// </summary>
    public class DefaultAgentContext : IAgentContext
    {
        /// <inheritdoc />
        /// <summary>
        /// The agent context wallet,
        /// </summary>
        public AriesStorage AriesStorage { get; set; }

        /// <inheritdoc />
        /// <summary>
        /// The agent context pool.
        /// </summary>
        public PoolAwaitable Pool { get; set; }

        /// <inheritdoc />
        /// <summary>
        /// The agent context state.
        /// </summary>
        public Dictionary<string, string> State { get; set; }

        /// <inheritdoc />
        /// <summary>
        /// A list of supported messages for the current agent.
        /// </summary>
        public IList<MessageType> SupportedMessages { get; set; }

        /// <summary>
        /// Gets or sets the value for UseMessageTypesHttps.
        /// Only affects messages created by the default services,
        /// if you create additional messages you have to set the useMessageTypesHttps via ctor too
        /// </summary>
        /// <value>True if to use UseMessageTypesHttps.</value>
        public bool UseMessageTypesHttps { get; set; }

        /// <summary>
        /// Gets or sets the configured agent for this context
        /// </summary>
        public IAgent Agent { get; set; }

        private readonly ConcurrentQueue<MessageContext> _queue = new();

        /// <summary>
        /// Adds a message to the current processing queue
        /// </summary>
        /// <param name="message">Message to queue</param>
        internal void AddNext(MessageContext message)
        {
            _queue.Enqueue(message);
        }

        internal bool TryGetNext(out MessageContext message)
        {
            return _queue.TryDequeue(out message);
        }
    }
}
