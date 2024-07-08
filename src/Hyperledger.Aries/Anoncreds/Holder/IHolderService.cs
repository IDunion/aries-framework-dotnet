using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Anoncreds.Holder
{
    public interface IHolderService
    {
        Task<string> GetMasterSecret();
    }
}
