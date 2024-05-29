using aries_askar_dotnet.AriesAskar;
using aries_askar_dotnet.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Hyperledger.Aries.AskarStore
{
    public class DefaultStoreService : IStoreService
    {
        public Task<string> GenerateRawKeyAsync(string Seed = null)
        {
            var key = StoreApi.GenerateRawKeyAsync(Seed);
            return key;
        }

        public async Task<Store> OpenStore(Store store, bool provision = false)
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
    }
}
