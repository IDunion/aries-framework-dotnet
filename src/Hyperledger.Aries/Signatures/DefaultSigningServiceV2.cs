using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Utils;
using System.Text;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Signatures
{
    internal class DefaultSigningServiceV2 : ISigningService
    {
        private IWalletRecordService _recordService;

        public DefaultSigningServiceV2(IWalletRecordService recordService)
        {
            _recordService = recordService;
        }

        public Task<byte[]> SignMessageAsync(IAgentContext context, string signingDid, string message)
        {
            return SignMessageAsync(context, signingDid, Encoding.UTF8.GetBytes(message));
        }

        public async Task<byte[]> SignMessageAsync(IAgentContext context, string signingDid, byte[] message)
        {
            string key = await DidUtils.KeyForLocalDidAsync(context, _recordService, signingDid);
            return await CryptoUtils.CreateSignatureAsync(context.AriesStorage, _recordService, key, message);
        }
    }
}
