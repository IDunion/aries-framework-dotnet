using aries_askar_dotnet;
using aries_askar_dotnet.Models;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.Handshakes.Common;
using Hyperledger.Aries.Features.Handshakes.Connection.Models;
using Hyperledger.Aries.Features.Handshakes.DidExchange;
using Hyperledger.Aries.Features.Routing;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Indy.CryptoApi;
using Hyperledger.Indy.PaymentsApi;
using Hyperledger.Indy.WalletApi;
using Multiformats.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stateless.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AriesAskarErrorCode = aries_askar_dotnet.ErrorCode;
using AriesAskarKey = aries_askar_dotnet.AriesAskar.KeyApi;
using SignatureType = aries_askar_dotnet.Models.SignatureType;

namespace Hyperledger.Aries.Utils
{
    public static class CryptoUtils
    {
        #region Constants
        public const string PROTECTED_HEADER_ENCRYPTION = "xchacha20poly1305_ietf";
        public const string PROTECTED_HEADER_TYP = "JWM/1.0";
        public const string PROTECTED_HEADER_AUTHCRYPT = "Authcrypt";
        public const string PROTECTED_HEADER_ANONCRYPT = "Anoncrypt";
        #endregion

        #region PackAsync
        [Obsolete("Deprecated in V2")]
        public static Task<byte[]> PackAsync(
           Wallet wallet, string recipientKey, byte[] message, string senderKey = null)
        {
            return PackAsync(wallet, new[] { recipientKey }, message, senderKey);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="recipientKey"></param>
        /// <param name="message"></param>
        /// <param name="senderKey"></param>
        /// <param name="recordService"></param>
        /// <returns></returns>
        public static Task<byte[]> PackAsync(
           AriesStorage storage, string recipientKey, byte[] message, string senderKey = null, IWalletRecordService recordService = null)
        {
            return PackAsync(storage, new[] { recipientKey }, message, senderKey, recordService);
        }

        [Obsolete("Deprecated in V2")]
        public static Task<byte[]> PackAsync(
            Wallet wallet, string[] recipientKeys, byte[] message, string senderKey = null)
        {
            return Crypto.PackMessageAsync(wallet, recipientKeys.ToJson(), senderKey, message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="recipientKeys"></param>
        /// <param name="message"></param>
        /// <param name="senderKey"></param>
        /// <param name="recordService"></param>
        /// <returns></returns>
        /// <exception cref="AriesFrameworkException"></exception>
        public static async Task<byte[]> PackAsync(
            AriesStorage storage, string[] recipientKeys, byte[] message, string senderKey = null, IWalletRecordService recordService = null)
        {
            if ((storage?.Wallet != null && storage?.Store != null) || (storage?.Wallet == null && storage?.Store == null))
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"Storage.Wallet is {storage?.Wallet} and Storage.Store is {storage?.Store}");
            }
            else if (storage?.Store != null)
            {
                return await PackMessageAsync(storage.Store, recipientKeys, senderKey, message, recordService);
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

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="storage"></param>
        /// <param name="recipientKey"></param>
        /// <param name="message"></param>
        /// <param name="senderKey"></param>
        /// <param name="recordService"></param>
        /// <returns></returns>
        public static Task<byte[]> PackAsync<T>(
            AriesStorage storage, string recipientKey, T message, string senderKey = null, IWalletRecordService recordService = null)
        {
            return PackAsync(storage, new[] { recipientKey }, message.ToByteArray(), senderKey, recordService);
        }

        [Obsolete("Deprecated in V2")]
        public static Task<byte[]> PackAsync<T>(
            Wallet wallet, string[] recipientKeys, T message, string senderKey = null)
        {
            return Crypto.PackMessageAsync(wallet, recipientKeys.ToJson(), senderKey, message.ToByteArray());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="storage"></param>
        /// <param name="recipientKeys"></param>
        /// <param name="message"></param>
        /// <param name="senderKey"></param>
        /// <param name="recordService"></param>
        /// <returns></returns>
        /// <exception cref="AriesFrameworkException"></exception>
        public static async Task<byte[]> PackAsync<T>(
            AriesStorage storage, string[] recipientKeys, T message, string senderKey = null, IWalletRecordService recordService = null)
        {
            if ((storage?.Wallet != null && storage?.Store != null) || (storage?.Wallet == null && storage?.Store == null))
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"Storage.Wallet is {storage?.Wallet} and Storage.Store is {storage?.Store}");
            }
            else if (storage?.Store != null)
            {
                return await PackMessageAsync(storage.Store, recipientKeys, senderKey, message.ToByteArray(), recordService);
            }
            else
            {
                return await Crypto.PackMessageAsync(storage.Wallet, recipientKeys.ToJson(), senderKey, message.ToByteArray());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="store"></param>
        /// <param name="recipientVerKeys"></param>
        /// <param name="senderVerKey"></param>
        /// <param name="unencryptedMessage"></param>
        /// <param name="recordService"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        private static async Task<byte[]> PackMessageAsync(Store store, string[] recipientVerKeys, string senderVerKey, byte[] unencryptedMessage, IWalletRecordService recordService)
        {
            if (recipientVerKeys == null || recipientVerKeys.Length <= 0)
            {
                throw new ArgumentNullException(nameof(recipientVerKeys));
            }

            IntPtr contentEncryptionKeyHandle = await AriesAskarKey.CreateKeyAsync(KeyAlg.XC20P, false);
            byte[] contentEncryptionKeyBytes = await AriesAskarKey.GetSecretBytesFromKeyAsync(contentEncryptionKeyHandle);
            var contentEncryptionKey = Multibase.Base58.Encode(contentEncryptionKeyBytes);

            string protectedInfo;

            if (!string.IsNullOrEmpty(senderVerKey))
            {
                // TODO: validate senderVerKey

                // AuthCrypt
                protectedInfo = await PrepareProtectedInfoAuthCrypt(store, recordService, contentEncryptionKey, recipientVerKeys, senderVerKey);
            }
            else
            {
                // AnonCrypt
                protectedInfo = await PrepareProtectedInfoAnonCrypt(contentEncryptionKey, recipientVerKeys);
            }


            byte[] nonce = await AriesAskarKey.CreateCryptoBoxRandomNonceAsync();
            (byte[] encryptedMessage, byte[] tag, _) = await AriesAskarKey.EncryptKeyWithAeadAsync(contentEncryptionKeyHandle, Encoding.UTF8.GetString(unencryptedMessage), nonce, protectedInfo);

            string msgWrapper = JsonConvert.SerializeObject(new MessageWrapper
            {
                Protected = protectedInfo,
                Iv = Convert.ToBase64String(nonce),
                Ciphertext = Convert.ToBase64String(encryptedMessage),
                Tag = Convert.ToBase64String(tag)
            });

            return Encoding.UTF8.GetBytes(msgWrapper);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="store"></param>
        /// <param name="recordService"></param>
        /// <param name="contentEncryptionKey"></param>
        /// <param name="recipientVerKeys"></param>
        /// <param name="senderVerkey"></param>
        /// <returns></returns>
        private static async Task<string> PrepareProtectedInfoAuthCrypt(Store store, IWalletRecordService recordService, string contentEncryptionKey, string[] recipientVerKeys, string senderVerkey)
        {

            IntPtr keyHandle = await recordService.GetKeyAsync(store, senderVerkey);
            byte[] secretKey = await AriesAskarKey.GetSecretBytesFromKeyAsync(keyHandle);
            // Key needs to be created with KeyAlg.ED25519 and then converted, due some native method bug.
            IntPtr senderKeyHandle = await AriesAskarKey.CreateKeyFromSecretBytesAsync(KeyAlg.ED25519, secretKey);
            IntPtr convertedSenderKeyHandle = await AriesAskarKey.ConvertKeyAsync(senderKeyHandle, KeyAlg.X25519);

            List<Recipient> recipients = new();

            foreach (string verKey in recipientVerKeys)
            {
                var decodedVerkey = Multibase.Base58.Decode(verKey);
                // Key needs to be created with KeyAlg.ED25519 and then converted, due some native method bug.
                IntPtr recipientKeyHandle = await AriesAskarKey.CreateKeyFromPublicBytesAsync(KeyAlg.ED25519, decodedVerkey);
                IntPtr convertedRecipientKeyHandle = await AriesAskarKey.ConvertKeyAsync(recipientKeyHandle, KeyAlg.X25519);
                byte[] nonce = await AriesAskarKey.CreateCryptoBoxRandomNonceAsync();
                // Encrypting the content encryption key.
                byte[] encryptedCek = await AriesAskarKey.CryptoBoxAsync(convertedRecipientKeyHandle, convertedSenderKeyHandle, contentEncryptionKey,nonce);
                // Encrypting the recipient verKey.
                byte[] encryptedSender = await AriesAskarKey.SealCryptoBoxAsync(convertedRecipientKeyHandle, senderVerkey);

                recipients.Add(new Recipient {
                    EncryptedKey = Multibase.Encode(MultibaseEncoding.Base64Url, encryptedCek),
                    Header = new RecipientHeader
                    {
                        Kid = verKey,
                        Sender = Multibase.Encode(MultibaseEncoding.Base64Url, encryptedSender),
                        Iv = Multibase.Encode(MultibaseEncoding.Base64Url, nonce),
                    }
                });
            }

            return await WrapProtectedInfo(recipients, true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="contentEncryptionKey"></param>
        /// <param name="recipientVerKeys"></param>
        /// <returns></returns>
        private static async Task<string> PrepareProtectedInfoAnonCrypt(string contentEncryptionKey, string[] recipientVerKeys)
        {
            List<Recipient> recipients = new();

            foreach(string verKey in recipientVerKeys)
            {
                IntPtr verKeyHandle = await AriesAskarKey.CreateKeyFromPublicBytesAsync(KeyAlg.ED25519, Multibase.Base58.Decode(verKey));              
                IntPtr convertedVerKeyHandle = await AriesAskarKey.ConvertKeyAsync(verKeyHandle, KeyAlg.X25519);
                byte[] encryptedCek = await AriesAskarKey.SealCryptoBoxAsync(convertedVerKeyHandle, contentEncryptionKey);
                
                recipients.Add(new Recipient
                {
                    EncryptedKey = Multibase.Encode(MultibaseEncoding.Base64Url, encryptedCek),
                    Header = new RecipientHeader
                    {
                        Kid = verKey,
                        Sender = null,
                        Iv = null
                    }
                });                
            }           

            return await WrapProtectedInfo(recipients, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="recipients"></param>
        /// <param name="isAuthCrypt"></param>
        /// <returns></returns>
        private static Task<string> WrapProtectedInfo(List<Recipient> recipients, bool isAuthCrypt = false)
        {
            return Task.FromResult(Multibase.Encode(MultibaseEncoding.Base64Url, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new ProtectedHeader {
                Enc = PROTECTED_HEADER_ENCRYPTION,
                Typ = PROTECTED_HEADER_TYP,
                Alg = isAuthCrypt ? PROTECTED_HEADER_AUTHCRYPT : PROTECTED_HEADER_ANONCRYPT,
                Recipients = recipients
            }))));
        }
        #endregion

        #region UnpackAsync
        [Obsolete("Deprecated in V2")]
        public static async Task<UnpackResult> UnpackAsync(Wallet wallet, byte[] message)
        {
            byte[] result = await Crypto.UnpackMessageAsync(wallet, message);
            return result.ToObject<UnpackResult>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="message"></param>
        /// <param name="recordService"></param>
        /// <returns></returns>
        /// <exception cref="AriesFrameworkException"></exception>
        public static async Task<UnpackResult> UnpackAsync(AriesStorage storage, byte[] message, IWalletRecordService recordService = null)
        {
            byte[] result;
            if ((storage?.Wallet != null && storage?.Store != null) || (storage?.Wallet == null && storage?.Store == null))
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"Storage.Wallet is {storage?.Wallet} and Storage.Store is {storage?.Store}");
            }
            else if (storage?.Store != null)
            {
                result = await UnpackMessageAsync(storage.Store, recordService, message);
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

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="storage"></param>
        /// <param name="message"></param>
        /// <param name="recordService"></param>
        /// <returns></returns>
        /// <exception cref="AriesFrameworkException"></exception>
        public static async Task<T> UnpackAsync<T>(AriesStorage storage, byte[] message, IWalletRecordService recordService = null)
        {
            byte[] result = null;
            if ((storage?.Wallet != null && storage?.Store != null) || (storage?.Wallet == null && storage?.Store == null))
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"Storage.Wallet is {storage?.Wallet} and Storage.Store is {storage?.Store}");
            }
            else if (storage?.Store != null)
            {
                result = await UnpackMessageAsync(storage.Store, recordService, message);
            }
            else
            {
                result = await Crypto.UnpackMessageAsync(storage.Wallet, message);
            }

            UnpackResult unpacked = result.ToObject<UnpackResult>();
            return unpacked.Message.ToObject<T>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="store"></param>
        /// <param name="recordService"></param>
        /// <param name="encryptedMessage"></param>
        /// <returns></returns>
        private static async Task<byte[]> UnpackMessageAsync(Store store, IWalletRecordService recordService, byte[] encryptedMessage)
        {
            string decodedMessage = Encoding.UTF8.GetString(encryptedMessage);
            MessageWrapper messageObject = JsonConvert.DeserializeObject<MessageWrapper>(decodedMessage);
            var tmpEncoding = MultibaseEncoding.Base64Url;
            byte[] headerBytes = null;
            Multibase.TryDecode(messageObject.Protected, out tmpEncoding, out headerBytes);
            string decodedHeader = Encoding.UTF8.GetString(headerBytes);
            ProtectedHeader headerObject = JsonConvert.DeserializeObject<ProtectedHeader>(decodedHeader);

            (Recipient recipient, bool isAuthCrypt) = await FindCorrectRecipient(store, recordService, headerObject);

            string contentEncryptionKey;
            string unencryptedSenderKey = null;
            if (isAuthCrypt)
            {
                (unencryptedSenderKey, contentEncryptionKey) = await UnpackCekAuthCrypt(store, recordService, recipient);
            }
            else
            {
                contentEncryptionKey = await UnpackCekAnonCrypt(store, recordService, recipient);
            }

            IntPtr contentEncryptionKeyHandle = await AriesAskarKey.CreateKeyFromSecretBytesAsync(KeyAlg.XC20P, Multibase.Base58.Decode(contentEncryptionKey));
            byte[] decryptedMessage = await AriesAskarKey.DecryptKeyWithAeadAsync(contentEncryptionKeyHandle,
                Convert.FromBase64String(messageObject.Ciphertext),
                Convert.FromBase64String(messageObject.Iv),
                Convert.FromBase64String(messageObject.Tag), 
                messageObject.Protected
            );

            return new UnpackResult
            {
                RecipientVerkey = recipient.Header.Kid,
                SenderVerkey = unencryptedSenderKey,
                Message = Encoding.UTF8.GetString(decryptedMessage),
            }.ToByteArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="store"></param>
        /// <param name="recordService"></param>
        /// <param name="header"></param>
        /// <returns></returns>
        private static async Task<(Recipient, bool)> FindCorrectRecipient(Store store, IWalletRecordService recordService, ProtectedHeader header)
        {
            Recipient foundRecipient = null;
            bool isAuthCrypt = false;

            foreach(Recipient recipient in header.Recipients)
            {
                IntPtr foundKey = await recordService.GetKeyAsync(store, recipient.Header.Kid);

                if (foundKey != new IntPtr())
                {
                    foundRecipient = recipient;
                    if(recipient.Header.Sender != null)
                    {
                        isAuthCrypt = true;
                    }
                    break;
                }
            }

            return (foundRecipient, isAuthCrypt);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="store"></param>
        /// <param name="recordService"></param>
        /// <param name="recipient"></param>
        /// <returns></returns>
        private static async Task<(string, string)> UnpackCekAuthCrypt(Store store, IWalletRecordService recordService, Recipient recipient)
        {
            var tmpEncoding = MultibaseEncoding.Base64Url;
            byte[] encryptedCek = null;
            Multibase.TryDecode(recipient.EncryptedKey, out tmpEncoding, out encryptedCek);
            byte[] usedIv = null;
            Multibase.TryDecode(recipient.Header.Iv, out tmpEncoding, out usedIv);
            byte[] encryptedSenderVerKey = null;
            Multibase.TryDecode(recipient.Header.Sender, out tmpEncoding, out encryptedSenderVerKey);

            IntPtr myKeyHandle = await recordService.GetKeyAsync(store, recipient.Header.Kid);
            IntPtr convertedMyKeyHandle = await AriesAskarKey.ConvertKeyAsync(myKeyHandle, KeyAlg.X25519);
            string unencryptedSenderVerKey = await AriesAskarKey.OpenSealCryptoBoxAsync(convertedMyKeyHandle, encryptedSenderVerKey);

            byte[] decodedSenderVerKey = Multibase.Base58.Decode(unencryptedSenderVerKey);
            IntPtr senderKeyHandle = await AriesAskarKey.CreateKeyFromPublicBytesAsync(KeyAlg.ED25519, decodedSenderVerKey);
            IntPtr convertedSenderKeyHandle = await AriesAskarKey.ConvertKeyAsync(senderKeyHandle, KeyAlg.X25519);
            string unencryptedCek = await AriesAskarKey.OpenCryptoBoxAsync(convertedMyKeyHandle, convertedSenderKeyHandle, encryptedCek, usedIv);

            return (unencryptedSenderVerKey, unencryptedCek);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="store"></param>
        /// <param name="recordService"></param>
        /// <param name="recipient"></param>
        /// <returns></returns>
        private static async Task<string> UnpackCekAnonCrypt(Store store, IWalletRecordService recordService, Recipient recipient)
        {
            var tmpEncoding = MultibaseEncoding.Base64Url;
            byte[] encryptedCek = null;
            Multibase.TryDecode(recipient.EncryptedKey, out tmpEncoding, out encryptedCek);

            IntPtr privateKeyHandle = await recordService.GetKeyAsync(store, recipient.Header.Kid);
            IntPtr convertedKeyHandle = await AriesAskarKey.ConvertKeyAsync(privateKeyHandle, KeyAlg.X25519);

            return await AriesAskarKey.OpenSealCryptoBoxAsync(convertedKeyHandle, encryptedCek);
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
        public static async Task<byte[]> PrepareAsync(IAgentContext agentContext, AgentMessage message, string recipientKey, string[] routingKeys = null, string senderKey = null, IWalletRecordService recordService = null)
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
            byte[] msg = await PackAsync(agentContext.AriesStorage, recipientKey, message.ToByteArray(), senderKey, recordService);

            string previousKey = recipientKey;

            if (routingKeys != null)
            {
                // TODO: In case of multiple key, should they each wrap a forward message
                // or pass all keys to the PackAsync function as array?
                foreach (string routingKey in routingKeys)
                {
                    string verkey = DidUtils.IsDidKey(routingKey) ? DidUtils.ConvertDidKeyToVerkey(routingKey) : routingKey;
                    // Anonpack
                    msg = await PackAsync(
                        agentContext.AriesStorage, 
                        verkey, 
                        new ForwardMessage(agentContext.UseMessageTypesHttps) 
                        { 
                            Message = JObject.Parse(msg.GetUTF8String()),
                            To = previousKey 
                        }.ToByteArray(),
                        recordService: recordService);
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
        public static Task<byte[]> PrepareAsync(IAgentContext agentContext, AgentMessage message, ConnectionRecord connection, IWalletRecordService recordService = null)
        {
            string recipientKey = connection.TheirVk
                ?? throw new AriesFrameworkException(ErrorCode.A2AMessageTransmissionError, "Cannot find encryption key");

            string[] routingKeys = connection.Endpoint?.Verkey != null ? connection.Endpoint.Verkey : new string[0];
            return PrepareAsync(agentContext, message, recipientKey, routingKeys, connection.MyVk, recordService);
        }

        #region CreateKey
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
        #endregion

        #region CreateSignature
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
        #endregion

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

    #region Additional Helper classes

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

    /// <summary>
    /// Used as data container in encryption process,
    /// </summary>
    public class Recipient
    {
        [JsonProperty("encrypted_key")]
        [JsonPropertyName("encrypted_key")]
        public string EncryptedKey { get; set; }
        [JsonProperty("header")]
        [JsonPropertyName("header")]
        public RecipientHeader Header { get; set; }
    }
    public class RecipientHeader
    {
        [JsonProperty("kid")]
        [JsonPropertyName("kid")]
        public string Kid { get; set; }
        [JsonProperty("sender")]
        [JsonPropertyName("sender")]
        public string Sender { get; set; }
        [JsonProperty("iv")]
        [JsonPropertyName("iv")]
        public string Iv { get; set; }
    }

    public class MessageWrapper
    {
        [JsonProperty("protected")]
        [JsonPropertyName("protected")]
        public string Protected { get; set; }
        [JsonProperty("iv")]
        [JsonPropertyName("iv")]
        public string Iv { get; set; }
        [JsonProperty("ciphertext")]
        [JsonPropertyName("ciphertext")]
        public string Ciphertext { get; set; }
        [JsonProperty("tag")]
        [JsonPropertyName("tag")]
        public string Tag { get; set; }
    }

    public class ProtectedHeader
    {
        [JsonProperty("enc")]
        [JsonPropertyName("enc")]
        public string Enc { get; set; }
        [JsonProperty("typ")]
        [JsonPropertyName("typ")]
        public string Typ { get; set; }
        [JsonProperty("alg")]
        [JsonPropertyName("alg")]
        public string Alg { get; set; }
        [JsonProperty("recipients")]
        [JsonPropertyName("recipients")]
        public IEnumerable<Recipient> Recipients { get; set; }
    }
    #endregion
}
