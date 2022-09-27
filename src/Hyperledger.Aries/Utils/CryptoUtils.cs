using aries_askar_dotnet;
using aries_askar_dotnet.Models;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.Handshakes.Common;
using Hyperledger.Aries.Features.Handshakes.DidExchange;
using Hyperledger.Aries.Features.Routing;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Indy.CryptoApi;
using Hyperledger.Indy.WalletApi;
using Multiformats.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AriesAskarErrorCode = aries_askar_dotnet.ErrorCode;
using AriesAskarKey = aries_askar_dotnet.AriesAskar.KeyApi;
using SignatureType = aries_askar_dotnet.Models.SignatureType;

namespace Hyperledger.Aries.Utils
{
    public static class CryptoUtils
    {
        #region PackAsync
        [Obsolete("Deprecated in V2")]
        public static Task<byte[]> PackAsync(
           Wallet wallet, string recipientKey, byte[] message, string senderKey = null)
        {
            return PackAsync(wallet, new[] { recipientKey }, message, senderKey);
        }

        public static Task<byte[]> PackAsync(
           AriesStorage storage, string recipientKey, byte[] message, string senderKey = null)
        {
            return PackAsync(storage, new[] { recipientKey }, message, senderKey);
        }

        [Obsolete("Deprecated in V2")]
        public static Task<byte[]> PackAsync(
            Wallet wallet, string[] recipientKeys, byte[] message, string senderKey = null)
        {
            return Crypto.PackMessageAsync(wallet, recipientKeys.ToJson(), senderKey, message);
        }

        public static async Task<byte[]> PackAsync(
            AriesStorage storage, string[] recipientKeys, byte[] message, string senderKey = null)
        {
            if ((storage?.Wallet != null && storage?.Store != null) || (storage?.Wallet == null && storage?.Store == null))
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"Storage.Wallet is {storage?.Wallet} and Storage.Store is {storage?.Store}");
            }
            else if (storage?.Store != null)
            {
                return await PackMessageAsync(storage.Store, recipientKeys.ToJson(), senderKey, message.ToByteArray());
            }
            else
            {
                return await Crypto.PackMessageAsync(storage.Wallet, recipientKeys.ToJson(), senderKey, message);
            }
        }

        [Obsolete("Deprecated in V2")]
        public static Task<byte[]> PackAsync<T>(
            Wallet wallet, string recipientKey, T message, string senderKey = null)
        {
            return PackAsync(wallet, new[] { recipientKey }, message.ToByteArray(), senderKey);
        }

        public static Task<byte[]> PackAsync<T>(
            AriesStorage storage, string recipientKey, T message, string senderKey = null)
        {
            return PackAsync(storage, new[] { recipientKey }, message.ToByteArray(), senderKey);
        }

        [Obsolete("Deprecated in V2")]
        public static Task<byte[]> PackAsync<T>(
            Wallet wallet, string[] recipientKeys, T message, string senderKey = null)
        {
            return Crypto.PackMessageAsync(wallet, recipientKeys.ToJson(), senderKey, message.ToByteArray());
        }

        public static async Task<byte[]> PackAsync<T>(
            AriesStorage storage, string[] recipientKeys, T message, string senderKey = null)
        {
            if ((storage?.Wallet != null && storage?.Store != null) || (storage?.Wallet == null && storage?.Store == null))
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"Storage.Wallet is {storage?.Wallet} and Storage.Store is {storage?.Store}");
            }
            else if (storage?.Store != null)
            {
                return await PackMessageAsync(storage.Store, recipientKeys.ToJson(), senderKey, message.ToByteArray());
            }
            else
            {
                return await Crypto.PackMessageAsync(storage.Wallet, recipientKeys.ToJson(), senderKey, message.ToByteArray());
            }
        }

        private static async Task<byte[]> PackMessageAsync(Store store, string recipientVk, string senderVk, byte[] unencryptedMessage)
        {
            IntPtr recipientHandle;
            IntPtr senderHandle = new IntPtr();

            if (!string.IsNullOrEmpty(recipientVk))
            {
                string recipientKey = JsonConvert.DeserializeObject<string[]>(recipientVk).FirstOrDefault();
                byte[] recipientBytes = Multibase.Base58.Decode(recipientKey);
                recipientHandle = await AriesAskarKey.CreateKeyFromPublicBytesAsync(KeyAlg.ED25519, recipientBytes);
            }
            else
            {
                throw new ArgumentNullException(nameof(recipientVk));
            }

            // Sender key can be null when anonymous packing.
            if (!string.IsNullOrEmpty(senderVk))
            {
                byte[] senderBytes = Multibase.Base58.Decode(senderVk);
                senderHandle = await AriesAskarKey.CreateKeyFromPublicBytesAsync(KeyAlg.ED25519, senderBytes);
            }

            byte[] nonce = await AriesAskarKey.CreateCryptoBoxRandomNonceAsync();
            return await AriesAskarKey.CryptoBoxAsync(recipientHandle, senderHandle, Encoding.UTF8.GetString(unencryptedMessage), nonce);
        }
        #endregion

        #region UnpackAsync
        [Obsolete("Deprecated in V2")]
        public static async Task<UnpackResult> UnpackAsync(Wallet wallet, byte[] message)
        {
            byte[] result = await Crypto.UnpackMessageAsync(wallet, message);
            return result.ToObject<UnpackResult>();
        }

        public static async Task<UnpackResult> UnpackAsync(AriesStorage storage, byte[] message)
        {
            byte[] result;
            if ((storage?.Wallet != null && storage?.Store != null) || (storage?.Wallet == null && storage?.Store == null))
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"Storage.Wallet is {storage?.Wallet} and Storage.Store is {storage?.Store}");
            }
            else if (storage?.Store != null)
            {
                result = await UnpackMessageAsync(storage.Store, message);
            }
            else
            {
                result = await Crypto.UnpackMessageAsync(storage.Wallet, message);
            }

            return result.ToObject<UnpackResult>();
        }

        [Obsolete("Deprecated in V2")]
        public static async Task<T> UnpackAsync<T>(Wallet wallet, byte[] message)
        {
            byte[] result = await Crypto.UnpackMessageAsync(wallet, message);
            UnpackResult unpacked = result.ToObject<UnpackResult>();
            return unpacked.Message.ToObject<T>();
        }

        public static async Task<T> UnpackAsync<T>(AriesStorage storage, byte[] message)
        {
            byte[] result = null;
            if ((storage?.Wallet != null && storage?.Store != null) || (storage?.Wallet == null && storage?.Store == null))
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"Storage.Wallet is {storage?.Wallet} and Storage.Store is {storage?.Store}");
            }
            else if (storage?.Store != null)
            {
                result = await UnpackMessageAsync(storage.Store, message);
            }
            else
            {
                result = await Crypto.UnpackMessageAsync(storage.Wallet, message);
            }

            UnpackResult unpacked = result.ToObject<UnpackResult>();
            return unpacked.Message.ToObject<T>();
        }

        private static async Task<byte[]> UnpackMessageAsync(Store store, byte[] encryptedMessage)
        {
            byte[] recipientBytes = Multibase.Base58.Decode("");
            IntPtr recipientHandle = await AriesAskarKey.CreateKeyFromPublicBytesAsync(KeyAlg.ED25519, recipientBytes);

            byte[] senderBytes = Multibase.Base58.Decode("");
            IntPtr senderHandle = await AriesAskarKey.CreateKeyFromPublicBytesAsync(KeyAlg.ED25519, senderBytes);

            byte[] nonce = null;
            var result = await AriesAskarKey.OpenCryptoBoxAsync(recipientHandle, senderHandle, encryptedMessage, nonce);
            return result.ToByteArray();
        }
        #endregion

        /// <summary>
        /// Generate unique random alpha-numeric key
        /// </summary>
        /// <param name="maxSize"></param>
        /// <returns></returns>
        public static string GetUniqueKey(int maxSize)
        {
            char[] chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();
            byte[] data = new byte[maxSize];
            using (RNGCryptoServiceProvider crypto = new())
            {
                crypto.GetNonZeroBytes(data);
            }

            StringBuilder result = new(maxSize);
            foreach (byte b in data)
            {
                _ = result.Append(chars[b % chars.Length]);
            }
            return result.ToString();
        }


        /// <summary>
        /// Prepares a wire level message from the application level agent message asynchronously 
        /// this includes packing the message and wrapping it in required forward messages
        /// if the message requires it.
        /// </summary>
        /// <param name="agentContext">The agentContext.</param>
        /// <param name="message">The message context.</param>
        /// <param name="recipientKey">The key to encrypt the message for.</param>
        /// <param name="routingKeys">The routing keys to pack the message for.</param>
        /// <param name="senderKey">The sender key to encrypt the message from.</param>
        /// <returns>The response async.</returns>
        public static async Task<byte[]> PrepareAsync(IAgentContext agentContext, AgentMessage message, string recipientKey, string[] routingKeys = null, string senderKey = null)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (recipientKey == null)
            {
                throw new ArgumentNullException(nameof(recipientKey));
            }

            recipientKey = DidUtils.IsDidKey(recipientKey) ? DidUtils.ConvertDidKeyToVerkey(recipientKey) : recipientKey;

            // Pack application level message
            byte[] msg = await PackAsync(agentContext.AriesStorage, recipientKey, message.ToByteArray(), senderKey);

            string previousKey = recipientKey;

            if (routingKeys != null)
            {
                // TODO: In case of multiple key, should they each wrap a forward message
                // or pass all keys to the PackAsync function as array?
                foreach (string routingKey in routingKeys)
                {
                    string verkey = DidUtils.IsDidKey(routingKey) ? DidUtils.ConvertDidKeyToVerkey(routingKey) : routingKey;
                    // Anonpack
                    msg = await PackAsync(agentContext.AriesStorage, verkey, new ForwardMessage(agentContext.UseMessageTypesHttps) { Message = JObject.Parse(msg.GetUTF8String()), To = previousKey }.ToByteArray());
                    previousKey = verkey;
                }
            }

            return msg;
        }

        /// <summary>
        /// Prepares a wire level message from the application level agent message for a connection asynchronously 
        /// this includes packing the message and wrapping it in required forward messages
        /// if the message requires it.
        /// </summary>
        /// <param name="agentContext">The agentContext.</param>
        /// <param name="message">The message context.</param>
        /// <param name="connection">The connection to prepare the message for.</param>
        /// <returns>The response async.</returns>
        public static Task<byte[]> PrepareAsync(IAgentContext agentContext, AgentMessage message, ConnectionRecord connection)
        {
            string recipientKey = connection.TheirVk
                ?? throw new AriesFrameworkException(ErrorCode.A2AMessageTransmissionError, "Cannot find encryption key");

            string[] routingKeys = connection.Endpoint?.Verkey != null ? connection.Endpoint.Verkey : new string[0];
            return PrepareAsync(agentContext, message, recipientKey, routingKeys, connection.MyVk);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="recordService"></param>
        /// <param name="seed"></param>
        /// <param name="cryptoType"></param>
        /// <returns></returns>
        /// <exception cref="AriesFrameworkException"></exception>
        public static async Task<string> CreateKeyAsync(AriesStorage storage, IWalletRecordService recordService, string seed = null, string cryptoType = "ed25519")
        {
            if ((storage?.Wallet != null && storage?.Store != null) || (storage?.Wallet == null && storage?.Store == null))
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"Storage.Wallet is {storage?.Wallet} and Storage.Store is {storage?.Store}");
            }
            else if (storage?.Store != null)
            {
                return await CreateKeyStore(storage, recordService, seed, cryptoType);
            }
            else if (storage?.Wallet != null)
            {
                JsonSerializerSettings settings = new()
                {
                    NullValueHandling = NullValueHandling.Ignore
                };
                cryptoType = cryptoType != null ? cryptoType == "ed25519" ? cryptoType : null : null;
                return await CreateKeyWallet(
                    storage.Wallet,
                    JsonConvert.SerializeObject(
                        new { seed, crypto_type = cryptoType },
                        settings
                        )
                    );
            }
            else
            {
                return null;
            }
        }

        private static async Task<string> CreateKeyStore(AriesStorage storage, IWalletRecordService recordService, string seed, string cryptoType)
        {
            KeyAlg keyAlg = cryptoType.ToLower() switch
            {
                "ed25519" => KeyAlg.ED25519,
                "bls12381g2" => KeyAlg.BLS12_381_G2,
                _ => KeyAlg.ED25519,
            };

            IntPtr keyHandle = await CreateKeyPair(keyAlg, seed);

            //if (string.IsNullOrEmpty(seed))
            //   seed = GetUniqueKey(32);

            //IntPtr keyHandle = await AriesAskarKey.CreateKeyFromSeedAsync(
            //    keyAlg: keyAlg,
            //    seed: seed,
            //   SeedMethod.BlsKeyGen);

            byte[] verKey = await AriesAskarKey.GetPublicBytesFromKeyAsync(keyHandle);
            string verKeyBase58 = Multibase.Base58.Encode(verKey);
            if (cryptoType != "ed25519" && !string.IsNullOrEmpty(cryptoType))
            {
                verKeyBase58 = verKeyBase58 + ":" + cryptoType;
            }

            byte[] signKey = await AriesAskarKey.GetSecretBytesFromKeyAsync(keyHandle);
            string signKeyBase58 = Multibase.Base58.Encode(signKey);

            KeyRecord keyRecord = new()
            {
                Id = verKeyBase58,
                Verkey = verKeyBase58,
                Signkey = signKeyBase58
            };
            await recordService.AddAsync(storage, keyRecord);
            await recordService.AddKeyAsync(storage, keyHandle, verKeyBase58);

            return verKeyBase58;
        }

        private static async Task<string> CreateKeyWallet(Wallet wallet, string keyJson)
        {
            return await Crypto.CreateKeyAsync(wallet, keyJson);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="recordService"></param> 
        /// <param name="myVerkey"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <exception cref="AriesFrameworkException"></exception>
        public static async Task<byte[]> CreateSignatureAsync(AriesStorage storage, IWalletRecordService recordService, string myVerkey, byte[] message)
        {
            if ((storage?.Wallet != null && storage?.Store != null) || (storage?.Wallet == null && storage?.Store == null))
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"Storage.Wallet is {storage?.Wallet} and Storage.Store is {storage?.Store}");
            }
            else
            {
                return storage?.Store != null
                    ? await CreateSignatureStore(storage, recordService, myVerkey, message)
                    : storage?.Wallet != null ? await CreateSignatureWallet(storage.Wallet, myVerkey, message) : null;
            }
        }

        private static async Task<byte[]> CreateSignatureStore(AriesStorage storage, IWalletRecordService recordService, string myVerkey, byte[] message)
        {
            byte[] signature;
            IntPtr keyHandle = await recordService.GetKeyAsync(storage, myVerkey);
            signature = await AriesAskarKey.SignMessageFromKeyAsync(keyHandle, message, SignatureType.EdDSA);
            return signature;
        }

        private static async Task<byte[]> CreateSignatureWallet(Wallet wallet, string myVerkey, byte[] message)
        {
            return await Crypto.SignAsync(wallet, myVerkey, message);
        }

        #region VerifyAsync
        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="message"></param>
        /// <param name="signature"></param>
        /// <param name="storage"></param>
        /// <returns></returns>
        /// <exception cref="AriesFrameworkException"></exception>
        public static async Task<bool> VerifyAsync(AriesStorage storage, string key, byte[] message, byte[] signature)
        {
            return (storage?.Wallet != null && storage?.Store != null) || (storage?.Wallet == null && storage?.Store == null)
                ? throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"Storage.Wallet is {storage?.Wallet} and Storage.Store is {storage?.Store}")
                : storage?.Store != null ? await VerifyAsyncStore(key, message, signature) : await VerifyAsyncWallet(key, message, signature);
        }

        private static async Task<bool> VerifyAsyncStore(string theirVerkey, byte[] message, byte[] signature)
        {
            byte[] theirVKByte = Multibase.Base58.Decode(theirVerkey);
            IntPtr keyHandle = await AriesAskarKey.CreateKeyFromPublicBytesAsync(KeyAlg.ED25519, theirVKByte);
            return await AriesAskarKey.VerifySignatureFromKeyAsync(keyHandle, message, signature, SignatureType.EdDSA);
        }

        private static async Task<bool> VerifyAsyncWallet(string theirVerkey, byte[] message, byte[] signature)
        {
            return await Crypto.VerifyAsync(theirVerkey, message, signature);
        }
        #endregion


        public static async Task<IntPtr> CreateKeyPair(KeyAlg keyAlg, string seed = null)
        {
            if (keyAlg is not KeyAlg.ED25519 and not KeyAlg.BLS12_381_G2)
            {
                throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, $"Unsupported key algorithm {keyAlg}");
            }

            if (!string.IsNullOrEmpty(seed))
            {
                try
                {
                    if (keyAlg == KeyAlg.ED25519)
                    {
                        // Here the seed is equal to the secret key parameter
                        return await AriesAskarKey.CreateKeyFromSecretBytesAsync(
                            keyAlg: keyAlg,
                            secretBytes: await ValidateAndConvertSeed(seed));
                    }
                    else
                    {
                        // Here the seed is equal to a seed parameter
                        return await AriesAskarKey.CreateKeyFromSeedAsync(keyAlg, seed, SeedMethod.BlsKeyGen);
                    }
                }
                catch (AriesAskarException e)
                {
                    if (e.errorCode == AriesAskarErrorCode.Input)
                    {
                        throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, "Invalid seed for key generation");
                    }
                    else
                    {
                        throw new AriesAskarException(e.Message, e.errorCode);
                    }
                }
            }
            else
            {
                return await AriesAskarKey.CreateKeyAsync(keyAlg, ephemeral: false);
            }
        }

        private static Task<byte[]> ValidateAndConvertSeed(string seed)
        {
            byte[] seedBytes;

            if (string.IsNullOrEmpty(seed))
            {
                return Task.FromResult<byte[]>(null);
            }

            seedBytes = seed.Contains("=") ? Multibase.Base64.Decode(seed) : Encoding.ASCII.GetBytes(seed);

            return (seedBytes.Length == 32) ? Task.FromResult(seedBytes) : throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, "Seed value must be 32 bytes in length");
        }
    }

    /// <summary>
    /// Result object from <see cref="Crypto.UnpackMessageAsync"/>
    /// </summary>
    public class UnpackResult
    {
        /// <summary>Gets or sets the message encoded as UTF8 string.</summary>
        /// <value>The message.</value>
        [JsonProperty("message")]
        public string Message { get; set; }

        /// <summary>Gets or sets the sender verkey.</summary>
        /// <value>The sender verkey.</value>
        [JsonProperty("sender_verkey")]
        [JsonPropertyName("sender_verkey")]
        public string SenderVerkey { get; set; }

        /// <summary>Gets or sets the recipient verkey.</summary>
        /// <value>The recipient verkey.</value>
        [JsonProperty("recipient_verkey")]
        [JsonPropertyName("recipient_verkey")]
        public string RecipientVerkey { get; set; }
    }
}
