using Hyperledger.Aries.Common;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Ledger.Models;
using indy_vdr_dotnet.libindy_vdr;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Ledger
{
    public class DefaultPoolServiceV2 : IPoolService
    {
        /// <summary>Collection of active pool handles.</summary>
        protected static readonly ConcurrentDictionary<string, AriesPool> Pools =
            new();

        /// <summary>
        /// Concurrent collection of txn author agreements
        /// </summary>
        /// <returns></returns>
        protected static readonly ConcurrentDictionary<string, IndyTaa> Taas =
            new();

        /// <summary>
        /// Concurrent collection of acceptance mechanisms lists
        /// </summary>
        /// <returns></returns>
        protected static readonly ConcurrentDictionary<string, IndyAml> Amls =
            new();

        /// <inheritdoc />
        public async Task<AriesPool> GetPoolAsync(string poolName, int protocolVersion)
        {
            _ = await ModApi.SetProtocolVersionAsync(protocolVersion);

            return await GetPoolAsync(poolName);
        }

        /// <inheritdoc />
        public Task<AriesPool> GetPoolAsync(string poolName)
        {
            return Pools.TryGetValue(poolName, out AriesPool pool)
                ? Task.FromResult(pool)
                : throw new AriesFrameworkException(ErrorCode.PoolNotFound, $"Pool {poolName} not found");
        }

        /// <inheritdoc />
        public async Task<IndyTaa> GetTaaAsync(string poolName)
        {
            if (Taas.TryGetValue(poolName, out IndyTaa taa))
            {
                return taa;
            }

            AriesPool pool = await GetPoolAsync(poolName, 2);
            IntPtr req = await LedgerApi.BuildGetTxnAuthorAgreementRequestAsync();
            string res = await SubmitRequestAsync(PoolAwaitable.FromPool(pool), req);

            JObject jresponse = JObject.Parse(res);
            taa = jresponse["result"]["data"].HasValues
                ? new IndyTaa
                {
                    Text = jresponse["result"]["data"]["text"].ToString(),
                    Version = jresponse["result"]["data"]["version"].ToString()
                }
                : null;

            _ = Taas.TryAdd(poolName, taa);

            return taa;
        }

        /// <inheritdoc />
        public async Task<IndyAml> GetAmlAsync(string poolName, DateTimeOffset timestamp = default, string version = null)
        {
            if (Amls.TryGetValue(poolName, out IndyAml aml))
            {
                return aml;
            }

            AriesPool pool = await GetPoolAsync(poolName, 2);
            IntPtr req = await LedgerApi.BuildGetAcceptanceMechanismsRequestAsync(
                timestamp: timestamp == DateTimeOffset.MinValue ? -1 : timestamp.ToUnixTimeSeconds(),
                version: version);
            string res = await SubmitRequestAsync(PoolAwaitable.FromPool(pool), req);

            JObject jresponse = JObject.Parse(res);
            aml = jresponse["result"]["data"].HasValues ? jresponse["result"]["data"].ToObject<IndyAml>() : null;
            _ = Amls.TryAdd(poolName, aml);

            return aml;
        }

        /// <inheritdoc />
        public async Task CreatePoolAsync(string poolName, string genesisFile)
        {
            _ = await ModApi.SetProtocolVersionAsync(2);
            IntPtr poolHandle = await PoolApi.CreatePoolAsync(transactionsPath: genesisFile);

            _ = Pools.TryAdd(poolName, new AriesPool(poolHandle: poolHandle));
        }

        /// <inheritdoc />
        public async Task<string> SubmitRequestAsync(PoolAwaitable poolHandle, object requestHandle)
        {

            if ((await poolHandle).PoolHandle is IntPtr pHandle && requestHandle is IntPtr reqHandle)
            {
                string response = await PoolApi.SubmitPoolRequestAsync(pHandle, reqHandle);
                EnsureSuccessResponse(response);
                return response;
            }

            throw new NotImplementedException("Unsupported request handle");
        }

        private void EnsureSuccessResponse(string res)
        {
            JObject response = JObject.Parse(res);

            if (!response["op"]!.ToObject<string>()!.Equals("reply", StringComparison.OrdinalIgnoreCase))
            {
                throw new AriesFrameworkException(ErrorCode.LedgerOperationRejected, "Ledger operation rejected");
            }
        }
    }
}
