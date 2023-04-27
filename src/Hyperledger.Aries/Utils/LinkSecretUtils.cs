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
    public static class LinkSecretUtils
    {
        /// <summary>
        /// Creates a new master secret for use with <c>indy_sdk</c> or <c>anoncreds_rs</c> methods and stores it as <see cref="LinkSecretRecord"/> in the wallet. 
        /// </summary>
        /// <param name="storage">The indy-sdk or aries-askar Wallet.</param>
        /// <param name="recordService">The record service.</param>
        /// /// <param name="linkSecretId">The master secret id.</param>
        /// <returns>The master secret id for accessing the corresponding record.</returns>
        public static async Task<string> CreateAndStoreLinkSecretAsync(AriesStorage storage, IWalletRecordService recordService, string linkSecretId = null)
        {
            //Invalid combination of Wallet and Store in AriesStorage
            if ((storage?.Wallet != null && storage?.Store != null) || (storage?.Wallet == null && storage?.Store == null))
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"Storage.Wallet is {storage?.Wallet} and Storage.Store is {storage?.Store}");
            }
            //Store in AriesStorage (V2 used)
            else if (storage?.Store != null)
            {
                if (string.IsNullOrEmpty(linkSecretId))
                {
                    linkSecretId = Guid.NewGuid().ToString();
                }

                LinkSecretRecord linkSecretRecord = new()
                {
                    Id = linkSecretId,
                    LinkSecretJson = await Anoncreds.LinkSecretApi.CreateLinkSecretAsync()
                };

                await recordService.AddAsync(storage, linkSecretRecord);
                return linkSecretId;
            }
            //Wallet in AriesStorage (V1 used)
            else
            {
                return await AnonCreds.ProverCreateMasterSecretAsync(storage.Wallet, linkSecretId);
            }
        }

        /// <summary>
        /// Get a master secret from the wallet for use with <c>indy_shared_rs</c> methods. 
        /// </summary>
        /// <param name="id">The master secret id.</param>
        /// <param name="storage">The indy-sdk or aries-askar Wallet.</param>
        /// <param name="recordService">The record service.</param>
        /// <returns>The master secret as JSON.</returns>
        public static async Task<string> GetLinkSecretJsonAsync(AriesStorage storage, IWalletRecordService recordService, string id)
        {
            LinkSecretRecord record = await recordService.GetAsync<LinkSecretRecord>(storage, id);
            return record?.LinkSecretJson;
        }
    }
}
