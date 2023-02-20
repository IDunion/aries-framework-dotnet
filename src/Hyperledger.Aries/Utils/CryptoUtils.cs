using anoncreds_rs_dotnet.Models;
using aries_askar_dotnet;
using aries_askar_dotnet.AriesAskar;
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
        //public const string PROTECTED_HEADER_ENCRYPTION = "xchacha20poly1305_ietf";
        public const string PROTECTED_HEADER_ENCRYPTION = "chacha20poly1305_ietf";
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
                return await PackMessageAsync(storage, recipientKeys, senderKey, message, recordService);
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
                return await PackMessageAsync(storage, recipientKeys, senderKey, message.ToByteArray(), recordService);
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
        private static async Task<byte[]> PackMessageAsync(AriesStorage storage, string[] recipientVerKeys, string senderVerKey, byte[] unencryptedMessage, IWalletRecordService recordService)
        {
            if (recipientVerKeys == null || recipientVerKeys.Length <= 0)
            {
                throw new ArgumentNullException(nameof(recipientVerKeys));
            }
    
            IntPtr contentEncryptionKeyHandle = await AriesAskarKey.CreateKeyAsync(KeyAlg.C20P, false);
            byte[] contentEncryptionKey = await AriesAskarKey.GetSecretBytesFromKeyAsync(contentEncryptionKeyHandle);

            string protectedHeaderJson;

            if (!string.IsNullOrEmpty(senderVerKey))
            {
                //TODO : validate senderVerkey -> can be in format 'verkey' (ed25519) or 'verkey:cryptoType' (other crypto types). For now only Ed25519 supported 
                if (await DidUtils.ValidateVerkeyED25519(senderVerKey))
                {
                    // AuthCrypt
                    protectedHeaderJson = await PrepareProtectedInfoAuthCrypt(storage, recordService, contentEncryptionKey, recipientVerKeys, senderVerKey);
                }
                else 
                    throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, $"Provided sender verkey is no valid base58 encoded verkey : {senderVerKey}.");
            }
            else
            {
                // AnonCrypt
                protectedHeaderJson = await PrepareProtectedInfoAnonCrypt(contentEncryptionKey, recipientVerKeys);
            }

            (byte[] encryptedMessage, byte[] tag, byte[] nonce) = await AriesAskarKey.EncryptKeyWithAeadAsync(
                contentEncryptionKeyHandle, 
                Encoding.UTF8.GetString(unencryptedMessage), 
                null,
                protectedHeaderJson);

            string msgWrapperJson = JsonConvert.SerializeObject(new MessageWrapper
            {
                Protected = protectedHeaderJson,
                Iv = Multibase.EncodeRaw(MultibaseEncoding.Base64UrlPadded, nonce),
                Ciphertext = Multibase.EncodeRaw(MultibaseEncoding.Base64UrlPadded, encryptedMessage),
                Tag = Multibase.EncodeRaw(MultibaseEncoding.Base64UrlPadded, tag),
            });

            return Encoding.UTF8.GetBytes(msgWrapperJson);
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
        private static async Task<string> PrepareProtectedInfoAuthCrypt(AriesStorage storage, IWalletRecordService recordService, byte[] contentEncryptionKey, string[] recipientVerKeys, string senderVerkey)
        {
            IntPtr keyHandle = await recordService.GetKeyAsync(storage, senderVerkey);
            IntPtr convertedSenderKeyHandle = await AriesAskarKey.ConvertKeyAsync(keyHandle, KeyAlg.X25519);

            List<Recipient> recipients = new();

            foreach (string verKey in recipientVerKeys)
            {
                // In case verkey is in format 'verkey:cryptoType', KeyHandle is generated only from verkey bytes
                string vk = verKey;
                string cryptoType = null;
                if (verKey.Contains(':'))
                {
                    vk = verKey.Split(':')[0];
                    cryptoType = verKey.Split(':')[1];
                }

                var decodedVerkey = Multibase.Base58.Decode(vk);
                IntPtr recipientKeyHandle = await AriesAskarKey.CreateKeyFromPublicBytesAsync(
                    cryptoType == null ? KeyAlg.ED25519 : KeyAlgConverter.ToKeyAlg(cryptoType), 
                    decodedVerkey);
                IntPtr convertedRecipientKeyHandle = await AriesAskarKey.ConvertKeyAsync(recipientKeyHandle, KeyAlg.X25519);

                byte[] nonce = await AriesAskarKey.CreateCryptoBoxRandomNonceAsync();

                // Encrypting the content encryption key.
                byte[] encryptedCek = await AriesAskarKey.CryptoBoxAsync(
                    convertedRecipientKeyHandle, 
                    convertedSenderKeyHandle, 
                    contentEncryptionKey, 
                    nonce);

                // Encrypting the sender verKey.
                byte[] encryptedSender = await AriesAskarKey.SealCryptoBoxAsync(convertedRecipientKeyHandle, senderVerkey);

                recipients.Add(new Recipient {
                    EncryptedKey = Multibase.EncodeRaw(MultibaseEncoding.Base64UrlPadded, encryptedCek),
                    Header = new RecipientHeader
                    {
                        Kid = verKey,
                        Sender = Multibase.EncodeRaw(MultibaseEncoding.Base64UrlPadded, encryptedSender),
                        Iv = Multibase.EncodeRaw(MultibaseEncoding.Base64UrlPadded, nonce),
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
        private static async Task<string> PrepareProtectedInfoAnonCrypt(byte[] contentEncryptionKey, string[] recipientVerKeys)
        {
            List<Recipient> recipients = new();

            foreach(string verKey in recipientVerKeys)
            {
                // In case verkey is in format 'verkey:cryptoType', KeyHandle is generated only from verkey bytes
                string vk = verKey;
                string cryptoType = null;
                if (verKey.Contains(':'))
                {
                    vk = verKey.Split(':')[0];
                    cryptoType = verKey.Split(':')[1];
                }

                IntPtr verKeyHandle = await AriesAskarKey.CreateKeyFromPublicBytesAsync(
                    cryptoType == null ? KeyAlg.ED25519 : KeyAlgConverter.ToKeyAlg(cryptoType), 
                    Multibase.Base58.Decode(vk)); 

                IntPtr convertedVerKeyHandle = await AriesAskarKey.ConvertKeyAsync(verKeyHandle, KeyAlg.X25519);
                byte[] encryptedCek = await AriesAskarKey.SealCryptoBoxAsync(convertedVerKeyHandle, contentEncryptionKey);
                
                recipients.Add(new Recipient
                {
                    EncryptedKey = Multibase.EncodeRaw(MultibaseEncoding.Base64UrlPadded, encryptedCek),
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
            string protectedHeaderJson = JsonConvert.SerializeObject(new ProtectedHeader
            {
                Enc = PROTECTED_HEADER_ENCRYPTION,
                Typ = PROTECTED_HEADER_TYP,
                Alg = isAuthCrypt ? PROTECTED_HEADER_AUTHCRYPT : PROTECTED_HEADER_ANONCRYPT,
                Recipients = recipients
            });
            var wrapperProtectedInfoBytes = Encoding.UTF8.GetBytes(protectedHeaderJson);

            return Task.FromResult(Multibase.EncodeRaw(MultibaseEncoding.Base64UrlPadded, wrapperProtectedInfoBytes));
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
                result = await UnpackMessageAsync(storage, recordService, message);
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
                result = await UnpackMessageAsync(storage, recordService, message);
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
        private static async Task<byte[]> UnpackMessageAsync(AriesStorage storage, IWalletRecordService recordService, byte[] encryptedMessage)
        {
            string msgWrapperJson = Encoding.UTF8.GetString(encryptedMessage);
            MessageWrapper msgWrapper = JsonConvert.DeserializeObject<MessageWrapper>(msgWrapperJson);

            byte[] protectedHeaderBytes = Multibase.DecodeRaw(MultibaseEncoding.Base64UrlPadded, msgWrapper.Protected);
            string protectedHeaderJson = Encoding.UTF8.GetString(protectedHeaderBytes);
            ProtectedHeader protectedHeader = JsonConvert.DeserializeObject<ProtectedHeader>(protectedHeaderJson);

            (Recipient recipient, bool isAuthCrypt) = await FindCorrectRecipient(storage, recordService, protectedHeader);

            byte[] contentEncryptionKeyBytes;
            string unencryptedSenderKey = null;
            if (isAuthCrypt)
            {
                (unencryptedSenderKey, contentEncryptionKeyBytes) = 
                    await UnpackCekAuthCrypt(storage, recordService, recipient);
            }
            else
            {
                contentEncryptionKeyBytes = 
                    await UnpackCekAnonCrypt(storage, recordService, recipient);
            }

            IntPtr contentEncryptionKeyHandle = await AriesAskarKey.CreateKeyFromSecretBytesAsync(
                KeyAlg.C20P, 
                contentEncryptionKeyBytes);

            byte[] cipher = Multibase.DecodeRaw(MultibaseEncoding.Base64UrlPadded, msgWrapper.Ciphertext);
            byte[] iv = Multibase.DecodeRaw(MultibaseEncoding.Base64UrlPadded, msgWrapper.Iv);
            byte[] tag = Multibase.DecodeRaw(MultibaseEncoding.Base64UrlPadded, msgWrapper.Tag);

            byte[] decryptedMessage = await AriesAskarKey.DecryptKeyWithAeadAsync(
                contentEncryptionKeyHandle,
                cipher,
                iv,
                tag,
                msgWrapper.Protected
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
        private static async Task<(Recipient, bool)> FindCorrectRecipient(AriesStorage storage, IWalletRecordService recordService, ProtectedHeader header)
        {
            Recipient foundRecipient = null;
            bool isAuthCrypt = false;

            foreach(Recipient recipient in header.Recipients)
            {
                IntPtr foundKey = await recordService.GetKeyAsync(storage, recipient.Header.Kid);

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
        private static async Task<(string, byte[])> UnpackCekAuthCrypt(AriesStorage storage, IWalletRecordService recordService, Recipient recipient)
        {
            byte[] encryptedCek = Multibase.DecodeRaw(MultibaseEncoding.Base64UrlPadded, recipient.EncryptedKey);
            byte[] iv = Multibase.DecodeRaw(MultibaseEncoding.Base64UrlPadded, recipient.Header.Iv);
            byte[] encryptedSenderVerkey = Multibase.DecodeRaw(MultibaseEncoding.Base64UrlPadded, recipient.Header.Sender);

            IntPtr myKeyHandle = await recordService.GetKeyAsync(storage, recipient.Header.Kid);
            IntPtr convertedMyKeyHandle = await AriesAskarKey.ConvertKeyAsync(myKeyHandle, KeyAlg.X25519);
            string decryptedSenderVerKey = await AriesAskarKey.OpenSealCryptoBoxAsync(convertedMyKeyHandle, encryptedSenderVerkey);
            
            //TODO : validate senderVerkey -> can be in format 'verkey' (ed25519) or 'verkey:cryptoType' (other crypto types). For now only Ed25519 supported 
            if (!await DidUtils.ValidateVerkeyED25519(decryptedSenderVerKey))
                throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, $"Sender verkey is no valid base58 encoded verkey : {decryptedSenderVerKey}.");

            // In case verkey is in format 'verkey:cryptoType', senderKeyHandle is generated only from verkey bytes
            string decryptedSenderVK = decryptedSenderVerKey;
            string cryptoType = null;
            if (decryptedSenderVerKey.Contains(':'))
            {
                decryptedSenderVK = decryptedSenderVerKey.Split(':')[0];
                cryptoType = decryptedSenderVerKey.Split(':')[1];
            }

            byte[] decodedSenderVerKey = Multibase.Base58.Decode(decryptedSenderVK);

            IntPtr senderKeyHandle = await AriesAskarKey.CreateKeyFromPublicBytesAsync(
                cryptoType==null? KeyAlg.ED25519 : KeyAlgConverter.ToKeyAlg(cryptoType), 
                decodedSenderVerKey);

            IntPtr convertedSenderKeyHandle = await AriesAskarKey.ConvertKeyAsync(senderKeyHandle, KeyAlg.X25519);
            byte[] unencryptedCek = await AriesAskarKey.OpenCryptoBoxBytesAsync(convertedMyKeyHandle, convertedSenderKeyHandle, encryptedCek, iv);

            return (decryptedSenderVerKey, unencryptedCek);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="store"></param>
        /// <param name="recordService"></param>
        /// <param name="recipient"></param>
        /// <returns></returns>
        private static async Task<byte[]> UnpackCekAnonCrypt(AriesStorage storage, IWalletRecordService recordService, Recipient recipient)
        {
            IntPtr privateKeyHandle = await recordService.GetKeyAsync(storage, recipient.Header.Kid);
            IntPtr convertedKeyHandle = await AriesAskarKey.ConvertKeyAsync(privateKeyHandle, KeyAlg.X25519);

            byte[] encryptedCek = Multibase.DecodeRaw(MultibaseEncoding.Base64UrlPadded, recipient.EncryptedKey);

            return await AriesAskarKey.OpenSealCryptoBoxBytesAsync(convertedKeyHandle, encryptedCek);
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
        /// Creates a new public / private keypair and stores it in the <see cref ="AriesStorage.Wallet"/> or <see cref ="AriesStorage.Store"/>.
        /// </summary>
        /// <param name="storage">The storage containing the indy-sdk or aries-askar wallet.</param>
        /// <param name="recordService">An implementation of the walletRecordService.</param>
        /// <param name="seed">The seed; default is null.</param>
        /// <param name="cryptoType">The cryptoType; default is ed25519.</param>
        /// <returns>The verkey to retrieve the keypair from <see cref ="Wallet"/> or <see cref ="Store"/>.</returns>
        /// <exception cref="AriesFrameworkException">Throws when <see cref ="AriesStorage.Store"/> or <see cref ="AriesStorage.Wallet"/> are null.</exception>
        public static async Task<string> CreateAndStoreKeyAsync(AriesStorage storage, IWalletRecordService recordService, string seed = null, string cryptoType = "ed25519")
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
        /// Creates a new public / private keypair. 
        /// <para>
        /// Note: Keys / keypairs represented by <see cref ="IntPtr"/> are only used in V2 services and can be stored/retrieved in/from the <see cref ="AriesStorage.Store"/> via <see cref ="DefaultWalletRecordServiceV2"/>.
        /// </para>
        /// </summary>
        /// <param name="keyAlg">The key algorithm.</param>
        /// <param name="seed">The seed.</param>
        /// <returns>The local key handle for the keypair.</returns>
        /// <exception cref="AriesFrameworkException">Throws when <see cref ="KeyAlg"/> or seed is invalid.</exception>
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
        /// Create a signature for a given message and key with EdDSA.
        /// </summary>
        /// <param name="storage">The storage containing the indy-sdk or aries-askar wallet.</param>
        /// <param name="recordService">An implementation of the walletRecordService.</param> 
        /// <param name="myVerkey">The verkey to obtain the secret key from the wallet in order to create the signature.</param>
        /// <param name="message">The message to sign.</param>
        /// <returns>The signature.</returns>
        /// <exception cref="AriesFrameworkException">Throws when <see cref ="AriesStorage.Store"/> or <see cref ="AriesStorage.Wallet"/> are null.</exception>
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
        /// Verifies the signature of the message with EdDSA.
        /// </summary>
        /// <param name="key">The verkey for which the signature is verified against.</param>
        /// <param name="message">The message to verify.</param>
        /// <param name="signature">The signature of the message.</param>
        /// <param name="storage">The storage containing the indy-sdk or aries-askar wallet.</param>
        /// <returns>True if the verification was successfull, otherwise false.</returns>
        /// <exception cref="AriesFrameworkException">Throws when <see cref ="AriesStorage.Store"/> or <see cref ="AriesStorage.Wallet"/> are null.</exception>
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
