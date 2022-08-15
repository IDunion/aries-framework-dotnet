using aries_askar_dotnet.Models;
using Hyperledger.Indy.WalletApi;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.Ledger.Models
{
    public class AriesPool
    {
        /// <summary>
        /// The indy-sdk version of PoolAwaitable.
        /// </summary>
        public PoolAwaitable Pool { get; set; }

        /// <summary>
        /// The indy-vdr version of PoolAwaitable.
        /// </summary>
        public PoolHandleAwaitable PoolHandle { get; set; }
    }
}
