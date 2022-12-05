using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Ledger.Abstractions;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Aries.Utils;
using Polly;
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

        public async Task<string> SignRequestAsync(IAgentContext context, string submitterDid, string requestJson)
        {
            byte[] sig = await SignRequestAsync(context.AriesStorage, submitterDid, Encoding.UTF8.GetBytes(requestJson));
            return Convert.ToBase64String(sig);
        }

        public async Task<string> SignRequestAsync(AriesStorage storage, string submitterDid, string requestJson)
        {
            byte[] sig = await SignRequestAsync(storage, submitterDid, Encoding.UTF8.GetBytes(requestJson));
            return Convert.ToBase64String(sig);
        }

        private async Task<byte[]> SignRequestAsync(AriesStorage storage, string signingDid, byte[] message)
        {
            string key = await DidUtils.KeyForLocalDidAsync(storage, _recordService, signingDid);
            return await CryptoUtils.CreateSignatureAsync(storage, _recordService, key, message);
        }
    }
}
