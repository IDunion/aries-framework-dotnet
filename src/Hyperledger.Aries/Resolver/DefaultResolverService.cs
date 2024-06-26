using Hyperledger.Aries.Resolver.Abstrations;
using indy_vdr_dotnet.libindy_vdr;
using System;
using System.Threading.Tasks;
using Hyperledger.Aries.Ledger.Models;

namespace Hyperledger.Aries.Resolver
{
    public class DefaultResolverService : IResolverService
    {
        public async Task<string> DereferenceAsync(PoolAwaitable poolHandle, string did_url)
        {
            if ((await poolHandle).PoolHandle is IntPtr pHandle)
            {
                var response = await ResolverApi.DereferenceAsync(pHandle, did_url);
                return response;
            }
            throw new NotImplementedException("Unsupported request handle");
        }

        public async Task<string> ResolveAsync(PoolAwaitable poolHandle, string did)
        {
            if ((await poolHandle).PoolHandle is IntPtr pHandle)
            {
                var response = await ResolverApi.ResolveAsync(pHandle, did);
                return response;
            }
            throw new NotImplementedException("Unsupported request handle");
        }
    }
}
