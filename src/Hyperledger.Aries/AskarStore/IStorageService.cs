using aries_askar_dotnet.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Hyperledger.Aries.AskarStore
{
    public interface IStorageService
    {
        Task<bool> RemoveStoreAsync(Store store);
        Task<Store> OpenStore(Store store, bool provision =false);
        Task<string> GenerateRawKeyAsync(string Seed = null);
    }
}
