using Hyperledger.Aries.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using IndyVdrLedger = indy_vdr_dotnet.libindy_vdr.LedgerApi;
using IndyVdrMod = indy_vdr_dotnet.libindy_vdr.ModApi;
using IndyVdrPool = indy_vdr_dotnet.libindy_vdr.PoolApi;

namespace Hyperledger.Aries.Ledger
{
    /// <inheritdoc />
    public class NewPoolService : IPoolService
    {
        /// <summary>Collection of active pool handles.</summary>
        protected static readonly ConcurrentDictionary<string, IntPtr> Pools =
            new ConcurrentDictionary<string, IntPtr>();

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

        /// <summary>
        /// DEPRECATED. Use GetVdrPool() instead.
        /// </summary>
        /// <param name="poolName"></param>
        /// <param name="protocolVersion"></param>
        /// <returns>NULL</returns>
        public virtual async Task<Indy.PoolApi.Pool> GetPoolAsync(string poolName, int protocolVersion)
        {
            return null;
        }

        /// <summary>
        /// DEPRECATED. Use GetVdrPool() instead.
        /// </summary>
        /// <param name="poolName"></param>
        /// <returns>NULL</returns>
        public virtual async Task<Indy.PoolApi.Pool> GetPoolAsync(string poolName)
        {
            return null;
        }

        public virtual async Task<IntPtr> GetVdrPoolAsync(string poolName)
        {
            if (Pools.TryGetValue(poolName, out IntPtr poolHandle))
            {
                return poolHandle;
            }

            poolHandle = await IndyVdrPool.CreatePoolAsync();
            _ = Pools.TryAdd(poolName, poolHandle);
            return poolHandle;
        }

        /// <inheritdoc />
        public virtual async Task CreatePoolAsync(string poolName, string genesisFile)
        {
            string poolConfig = JsonConvert.SerializeObject(new
            {
                protocol_version = "2"
            });

            _ = await IndyVdrMod.SetConfigAsync(poolConfig);
            _ = await IndyVdrPool.CreatePoolAsync(transactionsPath: genesisFile);
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

            string poolConfig = JsonConvert.SerializeObject(new
            {
                protocol_version = "2"
            });

            _ = await IndyVdrMod.SetConfigAsync(poolConfig);
            IntPtr poolHandle = await IndyVdrPool.CreatePoolAsync();

            IntPtr req = await IndyVdrLedger.BuildGetTxnAuthorAgreementRequestAsync();
            string res = await IndyVdrPool.SubmitPoolRequestAsync(poolHandle, req);

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

            string poolConfig = JsonConvert.SerializeObject(new
            {
                protocol_version = "2"
            });

            _ = await IndyVdrMod.SetConfigAsync(poolConfig);
            IntPtr poolHandle = await IndyVdrPool.CreatePoolAsync();

            IntPtr req = await IndyVdrLedger.BuildGetAcceptanceMechanismsRequestAsync(
                submitterDid: null,
                timestamp: timestamp == DateTimeOffset.MinValue ? -1 : timestamp.ToUnixTimeSeconds(),
                version: version);
            string res = await IndyVdrPool.SubmitPoolRequestAsync(poolHandle, req);

            EnsureSuccessResponse(res);

            aml = ParseAml(res);
            _ = Amls.TryAdd(poolName, aml);
            return aml;
        }
    }
}
