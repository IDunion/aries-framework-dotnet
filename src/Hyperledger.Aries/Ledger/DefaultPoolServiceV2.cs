using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Ledger.Models;
using indy_vdr_dotnet.libindy_vdr;
using Newtonsoft.Json.Linq;

namespace Hyperledger.Aries.Ledger
{
    public class DefaultPoolServiceV2 : IPoolService
    {
        /// <summary>Collection of active pool handles.</summary>
        protected static readonly ConcurrentDictionary<string, AriesPool> Pools =
            new ConcurrentDictionary<string, AriesPool>();

        /// <summary>
        /// Concurrent collection of txn author agreements
        /// </summary>
        /// <returns></returns>
        protected static readonly ConcurrentDictionary<string, IndyTaa> Taas =
            new ConcurrentDictionary<string, IndyTaa>();

        /// <summary>
        /// Concurrent collection of acceptance mechanisms lists
        /// </summary>
        /// <returns></returns>
        protected static readonly ConcurrentDictionary<string, IndyAml> Amls =
            new ConcurrentDictionary<string, IndyAml>();

        /// <inheritdoc />
        public async Task<AriesPool> GetPoolAsync(string poolName, int protocolVersion)
        {
            await ModApi.SetProtocolVersionAsync(protocolVersion);

            return await GetPoolAsync(poolName);
        }

        /// <inheritdoc />
        public Task<AriesPool> GetPoolAsync(string poolName)
        {
            if (Pools.TryGetValue(poolName, out var pool))
            {
                return Task.FromResult(pool);
            }

            throw new AriesFrameworkException(ErrorCode.PoolNotFound, $"Pool {poolName} not found");
        }

        /// <inheritdoc />
        public async Task<IndyTaa> GetTaaAsync(string poolName)
        {
            if (Taas.TryGetValue(poolName, out var taa))
            {
                return taa;
            }

            var pool = await GetPoolAsync(poolName, 2);
            var req = await LedgerApi.BuildGetTxnAuthorAgreementRequestAsync();
            var res = await SubmitRequestAsync(PoolAwaitable.FromPool(pool), req);

            var jobj = JObject.Parse(res);
            taa = new IndyTaa
            {
                Text = jobj["result"]!["data"]!["text"]!.ToString(),
                Version = jobj["result"]!["data"]!["version"]!.ToString()
            };
            Taas.TryAdd(poolName, taa);

            return taa;
        }

        /// <inheritdoc />
        public async Task<IndyAml> GetAmlAsync(string poolName, DateTimeOffset timestamp = default, string version = null)
        {
            if (Amls.TryGetValue(poolName, out var aml))
            {
                return aml;
            }

            var pool = await GetPoolAsync(poolName, 2);
            var req = await LedgerApi.BuildGetAcceptanceMechanismsRequestAsync(
                timestamp: timestamp == DateTimeOffset.MinValue ? -1 : timestamp.ToUnixTimeSeconds(),
                version: version);
            var res = await SubmitRequestAsync(PoolAwaitable.FromPool(pool), req);

            var jobj = JObject.Parse(res);
            return jobj["result"]!["data"]!.ToObject<IndyAml>();
        }

        /// <inheritdoc />
        public async Task CreatePoolAsync(string poolName, string genesisFile)
        {
            await ModApi.SetProtocolVersionAsync(2);
            var poolHandle = await PoolApi.CreatePoolAsync(transactionsPath: genesisFile);

            Pools.TryAdd(poolName, new AriesPool(poolHandle: poolHandle));
        }

        /// <inheritdoc />
        public async Task<string> SubmitRequestAsync(PoolAwaitable poolHandle, object requestHandle)
        {

            if ((await poolHandle).PoolHandle is IntPtr pHandle && requestHandle is IntPtr reqHandle)
            {
                var response = await PoolApi.SubmitPoolRequestAsync(pHandle, reqHandle);
                EnsureSuccessResponse(response);
                return response;
            }

            throw new NotImplementedException("Unsupported request handle");
        }

        private void EnsureSuccessResponse(string res)
        {
            var response = JObject.Parse(res);

            if (!response["op"]!.ToObject<string>()!.Equals("reply", StringComparison.OrdinalIgnoreCase))
                throw new AriesFrameworkException(ErrorCode.LedgerOperationRejected, "Ledger operation rejected");
        }
    }
}
