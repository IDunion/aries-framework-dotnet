using Hyperledger.Indy.PoolApi;
using System;

namespace Hyperledger.Aries.Ledger.Models
{
    public class AriesPool
    {
        /// <summary>
        /// The indy-sdk version of Pool.
        /// </summary>
        public Pool Pool { get; set; }

        /// <summary>
        /// The indy-vdr version of Pool handle.
        /// </summary>
        public IntPtr PoolHandle { get; set; }

        public AriesPool(Pool pool = null, IntPtr poolHandle = default)
        {
            Pool = pool;
            PoolHandle = poolHandle;
        }
    }
}
