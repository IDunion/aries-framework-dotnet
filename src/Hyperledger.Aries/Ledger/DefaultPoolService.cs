using Hyperledger.Aries.Common;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Ledger.Models;
using Hyperledger.Indy.PoolApi;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using IndyLedger = Hyperledger.Indy.LedgerApi.Ledger;

namespace Hyperledger.Aries.Ledger
{
    /// <inheritdoc />
    public class DefaultPoolService : IPoolService
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
        public virtual async Task<AriesPool> GetPoolAsync(string poolName, int protocolVersion)
        {
            await Pool.SetProtocolVersionAsync(protocolVersion);

            return await GetPoolAsync(poolName);
        }

        /// <inheritdoc />
        public virtual async Task<AriesPool> GetPoolAsync(string poolName)
        {
            if (Pools.TryGetValue(poolName, out AriesPool ariesPool))
            {
                return ariesPool;
            }
            ariesPool = new AriesPool
            {
                Pool = await Pool.OpenPoolLedgerAsync(poolName, null)
            };

            _ = Pools.TryAdd(poolName, ariesPool);
            return ariesPool;
        }

        /// <inheritdoc />
        public virtual async Task CreatePoolAsync(string poolName, string genesisFile)
        {
            string poolConfig = new { genesis_txn = genesisFile }.ToJson();

            await Pool.CreatePoolLedgerConfigAsync(poolName, poolConfig);
        }

        /// <inheritdoc />
        public Task<string> SubmitRequestAsync(PoolAwaitable poolHandle, object requestHandle)
        {
            throw new NotImplementedException($"{nameof(SubmitRequestAsync)} is not implemented within DefaultLedgerService; use DefaultLedgerServiceV2 instead");
        }

        /// <inheritdoc />
        public async Task<IndyTaa> GetTaaAsync(string poolName)
        {
            static IndyTaa ParseTaa(string response)
            {
                JObject jresponse = JObject.Parse(response);
                return jresponse["result"]["data"].HasValues
                    ? new IndyTaa
                    {
                        Text = jresponse["result"]["data"]["text"].ToString(),
                        Version = jresponse["result"]["data"]["version"].ToString()
                    }
                    : null;
            };

            if (Taas.TryGetValue(poolName, out IndyTaa taa))
            {
                return taa;
            }

            AriesPool ariesPool = await GetPoolAsync(poolName, 2);
            string req = await IndyLedger.BuildGetTxnAuthorAgreementRequestAsync(null, null);
            string res = await IndyLedger.SubmitRequestAsync(ariesPool.Pool, req);

            EnsureSuccessResponse(res);

            taa = ParseTaa(res);
            _ = Taas.TryAdd(poolName, taa);
            return taa;
        }

        private void EnsureSuccessResponse(string res)
        {
            JObject response = JObject.Parse(res);

            if (!response["op"].ToObject<string>().Equals("reply", StringComparison.OrdinalIgnoreCase))
            {
                throw new AriesFrameworkException(ErrorCode.LedgerOperationRejected, "Ledger operation rejected");
            }
        }

        /// <inheritdoc />
        public async Task<IndyAml> GetAmlAsync(string poolName, DateTimeOffset timestamp = default, string version = null)
        {
            static IndyAml ParseAml(string response)
            {
                JObject jresponse = JObject.Parse(response);
                return jresponse["result"]["data"].HasValues ? jresponse["result"]["data"].ToObject<IndyAml>() : null;
            };

            if (Amls.TryGetValue(poolName, out IndyAml aml))
            {
                return aml;
            }

            AriesPool ariesPool = await GetPoolAsync(poolName, 2);
            string req = await IndyLedger.BuildGetAcceptanceMechanismsRequestAsync(
                submitter_did: null,
                timestamp: timestamp == DateTimeOffset.MinValue ? -1 : timestamp.ToUnixTimeSeconds(),
                version: version);
            string res = await IndyLedger.SubmitRequestAsync(ariesPool.Pool, req);

            EnsureSuccessResponse(res);

            aml = ParseAml(res);
            _ = Amls.TryAdd(poolName, aml);
            return aml;
        }
    }
}
