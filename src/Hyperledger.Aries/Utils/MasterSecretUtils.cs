using System;
using IndySharedRsMs = indy_shared_rs_dotnet.IndyCredx.MasterSecretApi;
using System.Threading.Tasks;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Storage;
using aries_askar_dotnet.Models;
using Hyperledger.Aries.Storage.Models;

namespace Hyperledger.Aries.Utils
{
    public static class MasterSecretUtils
    {
        /// <summary>
        /// Creates a new master secret for use with <c>indy_shared_rs</c> methods and stores it as <see cref="MasterSecretRecord"/> in the wallet. 
        /// </summary>
        /// <param name="storage">The indy-sdk or aries-askar Wallet.</param>
        /// <param name="recordService">The record service.</param>
        /// <returns>The master secret id for accessing the corresponding record.</returns>
        public static async Task<string> CreateAndStoreMasterSecretAsync(AriesStorage storage, IWalletRecordService recordService)
        {
            string masterSecretId = Guid.NewGuid().ToString();
            MasterSecretRecord masterSecretRecord = new()
            {
                Id = masterSecretId,
                MasterSecretJson = await IndySharedRsMs.CreateMasterSecretJsonAsync()
            };

            await recordService.AddAsync(storage, masterSecretRecord);
            return masterSecretId;
        }

        /// <summary>
        /// Get a master secret from the wallet for use with <c>indy_shared_rs</c> methods. 
        /// </summary>
        /// <param name="id">The master secret id.</param>
        /// <param name="wallet">The wallet.</param>
        /// <param name="recordService">The record service.</param>
        /// <returns>The master secret as JSON.</returns>
        public static async Task<string> GetMasterSecretJsonAsync(Store wallet, IWalletRecordService recordService, string id)
        {
            MasterSecretRecord record = await recordService.GetAsync<MasterSecretRecord>(wallet, id);
            return record?.MasterSecretJson;
        }
    }
}
