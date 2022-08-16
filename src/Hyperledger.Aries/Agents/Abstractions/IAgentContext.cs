﻿using System.Collections.Generic;
using aries_askar_dotnet.Models;
using Hyperledger.Aries.Ledger;
using Hyperledger.Aries.Ledger.Models;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Indy.WalletApi;

namespace Hyperledger.Aries.Agents
{
    /// <summary>
    /// Represents an agent context
    /// </summary>
    public interface IAgentContext
    {
        /// <summary>Gets or sets the agent wallet.</summary>
        /// <value>The wallet.</value>
        Wallet Wallet { get; set; }

        /// <summary>Gets or sets the pool.</summary>
        /// <value>The pool.</value>
        PoolAwaitable Pool { get; set; }

        /// <summary>Gets or sets the agent wallet.</summary>
        /// <value>The aries-askar version of wallet.</value>
        Store WalletStore { get; set; }

        /// <summary>Gets or sets the pool handle.</summary>
        /// <value>The indy-vdr version of pool handle.</value>
        NewPoolAwaitable PoolHandle { get; set; }

        /// <summary>Name/value utility store to pass data
        /// along the execution pipeline.</summary>
        /// <value>The state.</value>
        Dictionary<string, string> State { get; set; }

        /// <summary>
        /// Gets or sets the supported messages of the current agent.
        /// </summary>
        IList<MessageType> SupportedMessages { get; set; }

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
        IAgent Agent { get; set; }
    }
}
