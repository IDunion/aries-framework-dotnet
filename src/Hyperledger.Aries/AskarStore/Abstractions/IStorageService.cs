using aries_askar_dotnet.Models;
using Hyperledger.Aries.AskarStore.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Hyperledger.Aries.AskarStore.Abstractions
{
    public interface IStorageService
    {
        Task<bool> RemoveStoreAsync(Store store);
        Task<Store> OpenStoreAsync(Store store, bool provision = false);
        Task<string> GenerateRawKeyAsync(string Seed = null);
        Task<string> CreateProfileAsync(Store store, string profile = null);
        Task<string> GetProfileNameAsync(Store store);
        Task<string> GetDefaultProfileAsync(Store store);
        Task<bool> SetDefaultProfileAsync(Store store, string profile);
        Task<bool> RemoveProfileAsync(Store store, string profile);
        Task<IEnumerable<AskarProfile>> GetListProfilesAsync(Store store);
    }
}
