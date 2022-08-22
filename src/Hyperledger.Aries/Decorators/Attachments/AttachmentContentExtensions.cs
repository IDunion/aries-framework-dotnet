using System;
using System.Text;
using System.Threading.Tasks;
using aries_askar_dotnet.Models;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Aries.Utils;
using Hyperledger.Indy.CryptoApi;
using Hyperledger.Indy.WalletApi;
using Multiformats.Base;
using AriesAskarKey = aries_askar_dotnet.AriesAskar.KeyApi;
using AriesAskarStore = aries_askar_dotnet.AriesAskar.StoreApi;
using AriesAskarResult = aries_askar_dotnet.AriesAskar.ResultListApi;
using Hyperledger.Aries.Decorators.Signature;
using Hyperledger.Aries.Agents;

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
        public static async Task SignWithJsonWebSignature(this AttachmentContent content, AriesStorage storage, string verkey)
        {
            if (!DidUtils.IsVerkey(verkey))
            {
                throw new ArgumentException("Not a valid verkey: " + verkey);
            }

            var payload = content.Base64;
            if (payload == null) throw new NullReferenceException("No data to sign");
            
            var did = DidUtils.ConvertVerkeyToDidKey(verkey);

            var protectedHeader = new
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

            var message = $"{protectedHeader}.{payload}";

            var signature = (await SignatureUtils.CreateSignature(storage, verkey, Encoding.ASCII.GetBytes(message))).ToBase64UrlString();

            content.JsonWebSignature = new JsonWebSignature
            {
                Header = new JsonWebSignatureHeader {Kid = did},
                Protected = protectedHeader,
                Signature = signature
            };
        }

        /// <summary>
        /// Sign attachment content using json web signature
        /// </summary>
        /// <param name="content">The attachment content to be signed.</param>
        /// <param name="store">The aries-askar store.</param>
        /// <param name="verkey">The verkey to be used for the signing.</param>
        /// <exception cref="NullReferenceException">Throws if payload is null.</exception>
        public static async Task SignWithJsonWebSignature(this AttachmentContent content, Store store, string verkey)
        {
            if (!DidUtils.IsVerkey(verkey))
            {
                throw new ArgumentException("Not a valid verkey: " + verkey);
            }

            var payload = content.Base64;
            if (payload == null) throw new NullReferenceException("No data to sign");

            var did = DidUtils.ConvertVerkeyToDidKey(verkey);

            var protectedHeader = new
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

            var message = $"{protectedHeader}.{payload}";

            if (store.session is null)
                _ = await AriesAskarStore.StartSessionAsync(store);

            IntPtr keyHandle = await AriesAskarResult.LoadLocalKeyHandleFromKeyEntryListAsync(await AriesAskarStore.FetchKeyAsync(store.session, verkey),0);
            /*** TODO : ??? - which SignatureType ? Could not find default from indy-sdk methods "indy_crypto_sign". probably EdDSA ***/
            var signature = (await AriesAskarKey.SignMessageFromKeyAsync(keyHandle , Encoding.ASCII.GetBytes(message), SignatureType.EdDSA)).ToBase64UrlString();
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
        public static async Task<bool> VerifyJsonWebSignature(this AttachmentContent content)
        {
            try
            {
                var did = content.JsonWebSignature.Header.Kid;
                
                var verkey = DidUtils.ConvertDidKeyToVerkey(did);
                
                var message = $"{content.JsonWebSignature.Protected}.{content.Base64}";
            
                return await Crypto.VerifyAsync(verkey, Encoding.ASCII.GetBytes(message),
                    content.JsonWebSignature.Signature.GetBytesFromBase64());
            }
            catch (Exception)
            {
                return false;
            }
        }
        /*** TODO : ??? - combine with VerifyJsonWebSignature and use AgentContext.AriesStoarge for distinguishing between Crypt.Verify and AriesAskarKey.VerifySignatureFromKeyAsync ***/
        /// <summary>
        /// Verify the json web signature of an attachment
        /// </summary>
        /// <param name="content">The attachment content to be verified.</param>
        /// <returns>True - signature is valid; False - signature is missing or invalid.</returns>
        public static async Task<bool> VerifyJsonWebSignatureAskar(this AttachmentContent content, IAgentContext agentContext)
        {
            try
            {
                var did = content.JsonWebSignature.Header.Kid;

                var verkey = DidUtils.ConvertDidKeyToVerkey(did);

                var message = $"{content.JsonWebSignature.Protected}.{content.Base64}";
                
                /*** TODO : ??? - which SignatureType / KeyAlg ?***/
                IntPtr publicVerKey = await AriesAskarKey.CreateKeyFromPublicBytesAsync(KeyAlg.ED25519, Multibase.Base58.Decode(verkey));
                return await AriesAskarKey.VerifySignatureFromKeyAsync(publicVerKey, Encoding.ASCII.GetBytes(message),
                    content.JsonWebSignature.Signature.GetBytesFromBase64(), SignatureType.EdDSA);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
