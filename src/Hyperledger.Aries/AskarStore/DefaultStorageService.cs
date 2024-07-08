using aries_askar_dotnet.AriesAskar;
using aries_askar_dotnet.Models;
using Hyperledger.Aries.AskarStore.Abstractions;
using Hyperledger.Aries.AskarStore.Models;
using Hyperledger.Aries.Extensions;
using Newtonsoft.Json.Linq;
using Stateless.Graph;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Hyperledger.Aries.AskarStore
{
    public class DefaultStorageService : IStorageService
    {
        public async Task<string> CreateProfileAsync(Store store, string profile = null)
        {
            return await store.CreateProfileAsync(profile);
        }

        public Task<string> GenerateRawKeyAsync(string Seed = null)
        {
            return StoreApi.GenerateRawKeyAsync(Seed);
        }

        public async Task<string> GetDefaultProfileAsync(Store store)
        {
            return await store.GetDefaultProfileAsync();
        }

        public async Task<string> GetProfileNameAsync(Store store)
        {
           return await store.GetProfileNameAsync();
        }
        public async Task<bool> RemoveProfileAsync(Store store, string profile)
        {
            return await store.RemoveProfileAsync(profile);
        }

        public async Task<Store> OpenStoreAsync(Store store, bool provision = false)
        {
            if (provision)
            {
                store = await StoreApi.ProvisionAsync(store.specUri);
            }
            else
            {
                store = await StoreApi.OpenAsync(store.specUri);
            }
          
            return store;
        }

        public Task<bool> RemoveStoreAsync(Store store)
        {
            return StoreApi.CloseAsync(store);
        }

        public async Task<bool> SetDefaultProfileAsync(Store store, string profile)
        {
           return await store.SetDefaultProfileAsync(profile);
        }

        public async Task<IEnumerable<AskarProfile>> GetListProfilesAsync(Store store)
        {
             var response =  await store.GetListProfilesAsync();
             var jobj = JArray.Parse(response.ToJson());
            var profileList = new List<AskarProfile>();
             foreach (var item in jobj)
            {
                profileList.Add(new AskarProfile
                {
                    Name = item["profile_id"].ToString()
                });
            }
             return profileList;
        }
    }
}
