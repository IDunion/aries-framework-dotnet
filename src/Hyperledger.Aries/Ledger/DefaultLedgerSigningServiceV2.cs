using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Ledger.Abstractions;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Aries.Utils;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Ledger
{
    internal class DefaultLedgerSigningServiceV2 : ILedgerSigningService
    {
        private readonly IWalletRecordService _recordService;

        public DefaultLedgerSigningServiceV2(IWalletRecordService recordService)
        {
            _recordService = recordService;
        }

        //public Task<byte[]> SignMessageAsync(IAgentContext context, string signingDid, string message)
        //{
        //    var sig = SignMessageAsync(context, signingDid, Encoding.UTF8.GetBytes(message)).GetAwaiter().GetResult();
        //    return Encoding.UTF8.GetString(sig, 0, sig.Length);
        //}

        //public async Task<byte[]> SignMessageAsync(IAgentContext context, string signingDid, byte[] message)
        //{
        //    string key = await DidUtils.KeyForLocalDidAsync(context.AriesStorage, _recordService, signingDid);
        //    return await CryptoUtils.CreateSignatureAsync(context.AriesStorage, _recordService, key, message);
        //}

        public async Task<string> SignRequestAsync(IAgentContext context, string submitterDid, string requestJson)
        {
            byte[] sig = await SignRequestAsync(context, submitterDid, Encoding.UTF8.GetBytes(requestJson));
            return Convert.ToBase64String(sig);
        }

        private async Task<byte[]> SignRequestAsync(IAgentContext context, string signingDid, byte[] message)
        {
            string key = await DidUtils.KeyForLocalDidAsync(context.AriesStorage, _recordService, signingDid);
            return await CryptoUtils.CreateSignatureAsync(context.AriesStorage, _recordService, key, message);
        }

        public Task<string> SignRequestAsync(AriesStorage storage, string submitterDid, string requestJson)
        {
            return SignRequestAsync(storage, submitterDid, requestJson);
        }
    }
}
