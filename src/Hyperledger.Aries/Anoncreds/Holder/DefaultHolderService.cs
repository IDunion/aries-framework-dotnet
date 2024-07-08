using anoncreds_rs_dotnet.Anoncreds;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Anoncreds.Holder
{
    public class DefaultHolderService : IHolderService
    {
        public async Task<string> GetMasterSecret()
        {
            return await LinkSecretApi.CreateLinkSecretAsync();
        }
    }
}
