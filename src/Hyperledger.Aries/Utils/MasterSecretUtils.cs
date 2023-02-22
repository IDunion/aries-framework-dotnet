using System;
using Anoncreds = anoncreds_rs_dotnet.Anoncreds;
using System.Threading.Tasks;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Indy.AnonCredsApi;
using Hyperledger.Aries.Common;

namespace Hyperledger.Aries.Utils
{
    public static class MasterSecretUtils
    {
        /// <summary>
        /// Creates a new master secret for use with <c>indy_sdk</c> or <c>anoncreds_rs</c> methods and stores it as <see cref="MasterSecretRecord"/> in the wallet. 
        /// </summary>
        /// <param name="storage">The indy-sdk or aries-askar Wallet.</param>
        /// <param name="recordService">The record service.</param>
        /// /// <param name="masterSecretId">The master secret id.</param>
        /// <returns>The master secret id for accessing the corresponding record.</returns>
        public static async Task<string> CreateAndStoreMasterSecretAsync(AriesStorage storage, IWalletRecordService recordService, string masterSecretId = null)
        {
            //Invalid combination of Wallet and Store in AriesStorage
            if ((storage?.Wallet != null && storage?.Store != null) || (storage?.Wallet == null && storage?.Store == null))
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"Storage.Wallet is {storage?.Wallet} and Storage.Store is {storage?.Store}");
            }
            //Store in AriesStorage (V2 used)
            else if (storage?.Store != null)
            {
                if (string.IsNullOrEmpty(masterSecretId))
                {
                    masterSecretId = Guid.NewGuid().ToString();
                }

                MasterSecretRecord masterSecretRecord = new()
                {
                    Id = masterSecretId,
                    MasterSecretJson = await Anoncreds.MasterSecretApi.CreateMasterSecretJsonAsync()
                };

                await recordService.AddAsync(storage, masterSecretRecord);
                return masterSecretId;
            }
            //Wallet in AriesStorage (V1 used)
            else
            {
                return await AnonCreds.ProverCreateMasterSecretAsync(storage.Wallet, masterSecretId);
            }
        }

        /// <summary>
        /// Get a master secret from the wallet for use with <c>indy_shared_rs</c> methods. 
        /// </summary>
        /// <param name="id">The master secret id.</param>
        /// <param name="storage">The indy-sdk or aries-askar Wallet.</param>
        /// <param name="recordService">The record service.</param>
        /// <returns>The master secret as JSON.</returns>
        public static async Task<string> GetMasterSecretJsonAsync(AriesStorage storage, IWalletRecordService recordService, string id)
        {
            MasterSecretRecord record = await recordService.GetAsync<MasterSecretRecord>(storage, id);
            return record?.MasterSecretJson;
        }
    }
}
