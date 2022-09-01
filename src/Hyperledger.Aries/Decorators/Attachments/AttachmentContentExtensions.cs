using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Aries.Utils;
using Multiformats.Base;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Decorators.Attachments
{
    public static class AttachmentContentExtensions
    {
        /*** TODO : ??? - Implement sign and verify for DefaultV2Services -> aries-askar KeyApi***/

        /// <summary>
        /// Sign attachment content using json web signature
        /// </summary>
        /// <param name="content">The attachment content to be signed.</param>
        /// <param name="storage">The Storage of Wallet or Store object.</param>
        /// <param name="verkey">The verkey to be used for the signing.</param>
        /// <exception cref="NullReferenceException">Throws if payload is null.</exception>
        public static async Task SignWithJsonWebSignature(this AttachmentContent content, AriesStorage storage, IWalletRecordService recordService, string verkey)
        {
            if (!DidUtils.IsVerkey(verkey))
            {
                throw new ArgumentException("Not a valid verkey: " + verkey);
            }

            string payload = content.Base64;
            if (payload == null)
            {
                throw new NullReferenceException("No data to sign");
            }

            string did = DidUtils.ConvertVerkeyToDidKey(verkey);

            string protectedHeader = new
            {
                alg = "EdDSA",
                kid = did,
                jwk = new
                {
                    kty = "OKP",
                    crv = "Ed25519",
                    x = Multibase.Base58.Decode(verkey).ToBase64UrlString(),
                    kid = did
                }
            }.ToJson().ToBase64Url();

            string message = $"{protectedHeader}.{payload}";

            string signature = (await CryptoUtils.CreateSignatureAsync(storage, recordService, verkey, Encoding.ASCII.GetBytes(message))).ToBase64UrlString();

            content.JsonWebSignature = new JsonWebSignature
            {
                Header = new JsonWebSignatureHeader { Kid = did },
                Protected = protectedHeader,
                Signature = signature
            };
        }

        /// <summary>
        /// Verify the json web signature of an attachment
        /// </summary>
        /// <param name="content">The attachment content to be verified.</param>
        /// <returns>True - signature is valid; False - signature is missing or invalid.</returns>
        public static async Task<bool> VerifyJsonWebSignature(this AttachmentContent content, IAgentContext agentContext)
        {
            try
            {
                string did = content.JsonWebSignature.Header.Kid;

                string verkey = DidUtils.ConvertDidKeyToVerkey(did);

                string message = $"{content.JsonWebSignature.Protected}.{content.Base64}";

                return await CryptoUtils.VerifyAsync(agentContext.AriesStorage, verkey, Encoding.ASCII.GetBytes(message),
                    content.JsonWebSignature.Signature.GetBytesFromBase64());
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
