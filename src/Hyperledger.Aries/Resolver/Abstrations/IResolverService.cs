using Hyperledger.Aries.Ledger;
using Hyperledger.Aries.Ledger.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Resolver.Abstrations
{
    public interface IResolverService
    {
        public  Task<string> ResolveAsync(PoolAwaitable poolHandle, string did);
        public  Task<string> DereferenceAsync(PoolAwaitable poolHandle, string did_url);
    }
}
