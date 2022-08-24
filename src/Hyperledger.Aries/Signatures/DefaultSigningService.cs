using System.Text;
using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Utils;
using Hyperledger.Indy.CryptoApi;
using Hyperledger.Indy.DidApi;

namespace Hyperledger.Aries.Signatures
{
    public class DefaultSigningService : ISigningService
    {
        public Task<byte[]> SignMessageAsync(IAgentContext context, string signingDid, string message)
        {
            return SignMessageAsync(context, signingDid, Encoding.UTF8.GetBytes(message));
        }
        
        public async Task<byte[]> SignMessageAsync(IAgentContext context, string signingDid, byte[] message)
        {
            var key = await Did.KeyForLocalDidAsync(context.AriesStorage.Wallet, signingDid);
            //await CryptoUtils.CreateSignatureAsync(context.AriesStorage, key, message);
            //return await Crypto.SignAsync(context.AriesStorage.Wallet, key, message);
            return await CryptoUtils.CreateSignatureAsync(context.AriesStorage, key, message);
        }
    }
}
