using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Aries.Utils;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Decorators.Signature
{
    /// <summary>
    /// Utility class for signing data for the usage in signature decorators.
    /// </summary>
    public static class SignatureUtils
    {
        /// <summary>
        /// Default signature type for signing data.
        /// </summary>
        public const string DefaultSignatureType =
            "did:sov:BzCbsNYhMrjHiqZDTUASHg;spec/signature/1.0/ed25519Sha512_single";

        /// <summary>
        /// Sign data supplied and return a signature decorator.
        /// </summary>
        /// <typeparam name="T">Data object type to sign.</typeparam>
        /// <param name="agentContext">Agent context.</param>
        /// <param name="data">Data to sign.</param>
        /// <param name="signerKey">Signers verkey.</param>
        /// <returns>Async signature decorator.</returns>
        public static async Task<SignatureDecorator> SignDataAsync<T>(IAgentContext agentContext, IWalletRecordService recordService, T data, string signerKey)
        {
            string dataJson = data.ToJson();
            byte[] epochData = BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            byte[] sigData = epochData.Concat(dataJson.GetUTF8Bytes()).ToArray();

            byte[] sig = await CryptoUtils.CreateSignatureAsync(agentContext.AriesStorage, recordService, signerKey, sigData);

            SignatureDecorator sigDecorator = new()
            {
                SignatureType = DefaultSignatureType,
                SignatureData = sigData.ToBase64UrlString(),
                Signature = sig.ToBase64UrlString(),
                Signer = signerKey
            };

            return sigDecorator;
        }

        /// <summary>
        /// Unpack and verify signed data before casting it to the supplied type.
        /// </summary>
        /// <typeparam name="T">Type in which to cast the result to.</typeparam>
        /// <param name="decorator">Signature decorator to unpack and verify</param>
        /// <returns>Resulting data cast to type T.</returns>
        public static async Task<T> UnpackAndVerifyAsync<T>(SignatureDecorator decorator, IAgentContext agentContext)
        {
            if (await CryptoUtils.VerifyAsync(agentContext.AriesStorage, decorator.Signer, decorator.SignatureData.GetBytesFromBase64(), decorator.Signature.GetBytesFromBase64()))
            {
                byte[] sigDataBytes = decorator.SignatureData.GetBytesFromBase64();
                string sigDataString = sigDataBytes.Skip(8).ToArray().GetUTF8String();
                return sigDataString.ToObject<T>();
            }
            throw new AriesFrameworkException(ErrorCode.InvalidMessage, "The signed payload was invalid");
        }
    }
}
