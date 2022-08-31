using System;
using System.IO;
using System.Threading.Tasks;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Ledger;
using Hyperledger.Indy.PoolApi;

namespace Hyperledger.TestHarness.Utils
{
    public class PoolUtils
    {
        private static IPoolService _poolService;
        private static Pool _pool;
        private static IntPtr _poolHandle;

        public static async Task<Pool> GetPoolAsync()
        {
            _poolService = new DefaultPoolService();
            if (_pool != null)
            {
                return _pool;
            }

            try
            {
                await _poolService.CreatePoolAsync("LocalTestPool", Path.GetFullPath("pool_genesis.txn"));
            }
            catch
            {
                // OK
            }
            return _pool = (await _poolService.GetPoolAsync("LocalTestPool", 2)).Pool;
        }

        public static async Task<IntPtr> GetPoolHandleAsync()
        {
            _poolService = new DefaultPoolServiceV2();
            if (_poolHandle != default)
            {
                return _poolHandle;
            }

            try
            {
                await _poolService.CreatePoolAsync("LocalTestPool", Path.GetFullPath("pool_genesis.txn"));
            }
            catch
            {
                // OK
            }
            return _poolHandle = (await _poolService.GetPoolAsync("LocalTestPool", 2)).PoolHandle;
        }
    }
}
