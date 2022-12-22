using aries_askar_dotnet.Models;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Features.Handshakes.DidExchange;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Indy.CryptoApi;
using Hyperledger.Indy.DidApi;
using Hyperledger.Indy.WalletApi;
using Multiformats.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AriesAskarKey = aries_askar_dotnet.AriesAskar.KeyApi;

namespace Hyperledger.Aries.Utils
{
    /// <summary>
    /// Did utilities
    /// </summary>
    public static class DidUtils
    {
        private const string FULL_VERKEY_REGEX = @"^[1-9A-HJ-NP-Za-km-z]{43,44}$";
        private const string ABREVIATED_VERKEY_REGEX = @"^~[1-9A-HJ-NP-Za-km-z]{22}$";
        private const string DID_REGEX = @"^did:([a-z]+):([a-zA-z\d]+)";
        private const string DID_KEY_REGEX = @"^did:key:([1-9A-HJ-NP-Za-km-z]+)";
        private const string DIDKEY_PREFIX = "did:key";
        private const string BASE58_PREFIX = "z";
        private static readonly byte[] MULTICODEC_PREFIX_ED25519 = { 0xed, 0x01 };

        /// <summary>
        /// Sovrin DID method spec.
        /// </summary>
        public const string DidSovMethodSpec = "sov";

        /// <summary>
        /// Did Key method spec.
        /// </summary>
        public const string DidKeyMethodSpec = "key";

        /// <summary>
        /// Did Indy method spec.
        /// </summary>
        public const string DidIndyMethodSpec = "indy";

        /// <summary>
        /// Constructs a DID from a method spec and identifier.
        /// </summary>
        /// <param name="methodSpec">DID method spec.</param>
        /// <param name="identifier">Identifier to use in DID.</param>
        /// <returns>DID.</returns>
        public static string ToDid(string methodSpec, string identifier)
        {
            return $"did:{methodSpec}:{identifier}";
        }

        /// <summary>
        /// Extracts the identifier from a DID.
        /// </summary>
        /// <param name="did">DID to extract the identifier from.</param>
        /// <returns>Identifier.</returns>
        public static string IdentifierFromDid(string did)
        {
            MatchCollection regExMatches = Regex.Matches(did, DID_REGEX);

            return regExMatches.Count != 1 || regExMatches[0].Groups.Count != 3 ? null : regExMatches[0].Groups[2].Value;
        }

        /// <summary>
        /// Extracts the method specification from a DID.
        /// </summary>
        /// <param name="did">DID to extract the method spec from.</param>
        /// <returns></returns>
        public static string MethodSpecFromDid(string did)
        {
            MatchCollection regExMatches = Regex.Matches(did, DID_REGEX);

            return regExMatches.Count != 1 || regExMatches[0].Groups.Count < 3 ? null : regExMatches[0].Groups[1].Value;
        }

        /// <summary>
        /// Check a base58 encoded string against a regex expression
        /// to determine if it is a full valid verkey
        /// </summary>
        /// <param name="verkey">Base58 encoded string representation of a verkey</param>
        /// <returns>Boolean indicating if the string is a valid verkey</returns>
        public static bool IsFullVerkey(string verkey)
        {
            return Regex.Matches(verkey, FULL_VERKEY_REGEX).Count == 1;
        }

        /// <summary>
        /// Check a base58 encoded string against a regex expression
        /// to determine if it is a abbreviated valid verkey
        /// </summary>
        /// <param name="verkey">Base58 encoded string representation of a abbreviated verkey</param>
        /// <returns>Boolean indicating if the string is a valid abbreviated verkey</returns>
        public static bool IsAbbreviatedVerkey(string verkey)
        {
            return Regex.Matches(verkey, ABREVIATED_VERKEY_REGEX).Count == 1;
        }

        /// <summary>
        /// Check a base58 encoded string to determine 
        /// if it is a valid verkey
        /// </summary>
        /// <param name="verkey">Base58 encoded string representation of a verkey</param>
        /// <returns>Boolean indicating if the string is a valid verkey</returns>

        public static bool IsVerkey(string verkey)
        {
            return IsAbbreviatedVerkey(verkey) || IsFullVerkey(verkey);
        }

        /// <summary>
        /// Check if a given string is a valid did:key representation
        /// </summary>
        /// <param name="didKey">Given string to check for did:key</param>
        /// <returns>Boolean indicating if the string is a valid did:key</returns>
        public static bool IsDidKey(string didKey)
        {
            return didKey != null && Regex.Matches(didKey, DID_KEY_REGEX).Count == 1;
        }

        /// <summary>
        /// Converts a base58 encoded ed25519 verkey into its did:key representation
        /// </summary>
        /// <param name="verkey">Base58 encoded string representation of a verkey</param>
        /// <returns>The did:key representation of a verkey as string</returns>
        public static string ConvertVerkeyToDidKey(string verkey)
        {
            if (!IsFullVerkey(verkey))
            {
                throw new ArgumentException($"Value {verkey} is no verkey", nameof(verkey));
            }

            byte[] bytes = Multibase.Base58.Decode(verkey);
            bytes = MULTICODEC_PREFIX_ED25519.Concat(bytes).ToArray();
            string base58PublicKey = Multibase.Base58.Encode(bytes);

            return $"{DIDKEY_PREFIX}:{BASE58_PREFIX}{base58PublicKey}";
        }

        /// <summary>
        /// Converts a did:key of a ed25519 public key into a plain base58 representation 
        /// </summary>
        /// <param name="didKey">A did:key representation of a ed25519 as string</param>
        /// <returns>A plain base58 representation of that public key</returns>
        public static string ConvertDidKeyToVerkey(string didKey)
        {
            if (!IsDidKey(didKey))
            {
                throw new ArgumentException($"Value {didKey} is no did:key", nameof(didKey));
            }

            string base58EncodedKey = didKey[$"{DIDKEY_PREFIX}:{BASE58_PREFIX}".Length..];
            byte[] bytes = Multibase.Base58.Decode(base58EncodedKey);
            byte[] codec = bytes.Take(MULTICODEC_PREFIX_ED25519.Length).ToArray();
            if (codec.SequenceEqual(MULTICODEC_PREFIX_ED25519))
            {
                bytes = bytes.Skip(MULTICODEC_PREFIX_ED25519.Length).ToArray();
                return Multibase.Base58.Encode(bytes);
            }

            throw new ArgumentException($"Value {didKey} has missing ED25519 multicodec prefix", nameof(didKey));
        }

        /// <summary>
        /// Ensure a given string represents a supported DID method.
        /// Will transform unqualified verkeys into did:key format.
        /// </summary>
        /// <param name="didCandidate"></param>
        /// <returns></returns>
        /// <exception cref="AriesFrameworkException"></exception>
        public static string EnsureQualifiedDid(string didCandidate)
        {
            return MethodSpecFromDid(didCandidate) is DidKeyMethodSpec or
                DidSovMethodSpec
                ? didCandidate
                : IsVerkey(didCandidate)
                ? ConvertVerkeyToDidKey(didCandidate)
                :
            throw new AriesFrameworkException(ErrorCode.UnsupportedDidMethod);
        }

        /// <summary>
        /// for use with <c>indy_shared_rs</c>.
        /// </summary>
        /// <remarks>
        /// <para>Saves the identity DID with keys in a wallet so that it can be used to sign
        /// and encrypt transactions.  Control over the created DID is provided through the 
        /// following parameter:
        /// </para>
        /// <para>The <c>did</c> member specifies the DID of the new entry.  If not 
        /// provided and the <c>cid</c> member is <c>false</c> then the first 16 bits of the VerKey value 
        /// generated will be used as a new DID.  If not provided and the <c>cid</c> member is <c>true</c> then the full 
        /// VerKey value will be used as a new DID.  If the <c>did</c> member is provided then the keys will be 
        /// replaced - this is normally used in the case of key rotation.</para>
        /// <para>The <c>seed</c> member specifies the seed to use when generating keys.  If not provided 
        /// then a random seed value will be created.</para>
        /// <para>The <c>crypto_type</c> member specifies the cryptographic algorithm used for generating
        /// keys.  If not provided then ed25519 curve is used.
        /// <note type="note">The only value currently supported for this member is 'ed25519'.</note>
        /// </para>
        /// <para>The <c>cid</c> member indicates whether the DID should be used in creating the DID.
        /// If not provided then the value defaults to false.</para>
        /// </remarks>
        /// <param name="storage">The storage</param>
        /// <param name="recordService"></param>
        /// <param name="did">The did as string; default null.</param>
        /// <param name="seed">The seed as string;default null.</param>
        /// <param name="cryptoType">The cryptoType as string; default "ed25519".</param>
        /// <param name="cid">The cid as bool; default false.</param>
        /// <returns>A tuple of strings. First is the did in format of "did":"method":"verkey". Second is the verkey </returns>
        /// <exception cref="AriesFrameworkException"></exception>
        public static async Task<(string, string)> CreateAndStoreMyDidAsync(
            AriesStorage storage,
            IWalletRecordService recordService,
            string did = null,
            string seed = null,
            string cryptoType = "ed25519",
            bool cid = false)
        {
            if ((storage?.Wallet != null && storage?.Store != null) || (storage?.Wallet == null && storage?.Store == null))
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"Storage.Wallet is {storage?.Wallet} and Storage.Store is {storage?.Store}");
            }
            else if (storage?.Store != null)
            {
                return await CreateAndStoreMyDidStore(storage, recordService, did, seed, cryptoType, cid);
            }
            else
            {
                JsonSerializerSettings settings = new()
                {
                    NullValueHandling = NullValueHandling.Ignore
                };
                cryptoType = cryptoType != null ? cryptoType == "ed25519" ? cryptoType : null : null;
                return await CreateAndStoreMyDidWallet(
                    storage.Wallet,
                    JsonConvert.SerializeObject(
                        new { did, seed, crypto_type = cryptoType, cid },
                        settings
                        )
                    );
            }
        }

        private static async Task<(string, string)> CreateAndStoreMyDidStore(AriesStorage storage,
            IWalletRecordService recordService,
            string did = null,
            string seed = null,
            string cryptoType = "ed25519",
            bool cid = false)
        {
            KeyAlg keyAlg = cryptoType.ToLower() switch
            {
                "ed25519" => KeyAlg.ED25519,
                "bls12381g2" => KeyAlg.BLS12_381_G2,
                _ => KeyAlg.ED25519,
            };

            IntPtr keyHandle = await CryptoUtils.CreateKeyPair(keyAlg, seed);

            byte[] verKey = await AriesAskarKey.GetPublicBytesFromKeyAsync(keyHandle);

            if (string.IsNullOrEmpty(did))
            {
                did = cid ? Multibase.Base58.Encode(verKey) : Multibase.Base58.Encode(verKey.Take(16).ToArray());
            }
            else
            {
                //Do nothing. did does not change only verKey and secretKey rotate
            }

            string verKeyBase58 = Multibase.Base58.Encode(verKey);
            if (cryptoType != "ed25519" && !string.IsNullOrEmpty(cryptoType))
            {
                verKeyBase58 = verKeyBase58 + ":" + cryptoType;
            }

            DidRecord didRecord = new()
            {
                Id = did,
                Did = did,
                Verkey = verKeyBase58
            };

            byte[] signKey = await AriesAskarKey.GetSecretBytesFromKeyAsync(keyHandle);
            string signKeyBase58 = Multibase.Base58.Encode(signKey);
            KeyRecord keyRecord = new()
            {
                Id = verKeyBase58,
                Verkey = verKeyBase58,
                Signkey = signKeyBase58
            };

            await recordService.AddAsync(storage, didRecord);
            await recordService.AddAsync(storage, keyRecord);
            await recordService.AddKeyAsync(storage, keyHandle, verKeyBase58);

            return (did, verKeyBase58);
        }

        private static async Task<(string, string)> CreateAndStoreMyDidWallet(Wallet wallet, string didJson)
        {
            CreateAndStoreMyDidResult did = await Did.CreateAndStoreMyDidAsync(wallet, didJson);
            return (did.Did, did.VerKey);
        }

        /// <summary>
        /// Stores a remote party's DID for a pairwise connection in the specified wallet.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The DID and optional associated parameters must be provided in the <paramref name="identityJson"/>
        /// parameter as a JSON <see cref="string"/>:
        /// </para>
        /// <code>
        /// {
        ///        "did": string, (required)
        ///        "verkey": string
        ///             - optional is case of adding a new DID, and DID is cryptonym: did == verkey,
        ///             - mandatory in case of updating an existing DID
        /// }
        /// </code>
        /// <para>The <c>did</c> member specifies the DID to store.  This value is required.</para>
        /// <para>The <c>verkey</c> member specifies the verification key and is optional.</para>
        /// <para>The <c>crypto_type</c> member specifies the type of cryptographic algorithm will be 
        /// used to generate they keys.  If not provided then ed22519 curve will be used.
        /// <note type="note">The only value currently supported for this member is 'ed25519'.</note>
        /// </para>
        /// </remarks>
        /// <param name="recordService"></param>
        /// <param name="storage">The wallet to store the DID in.</param>
        /// <param name="identityJson">The identity JSON.</param>
        /// <returns>An asynchronous <see cref="Task"/> that  with no return value the completes when the operation completes.</returns>
        public static async Task StoreTheirDidAsync(IWalletRecordService recordService, AriesStorage storage, string identityJson)
        {
            if (string.IsNullOrEmpty(identityJson))
            {
                throw new ArgumentNullException(nameof(identityJson));
            }

            DidRecord theirDid = await CreateTheirDidAsync(identityJson);
            await Upsert(recordService, storage, theirDid);
        }

        private static async Task<DidRecord> CreateTheirDidAsync(string identityJson)
        {
            DidRecord record = JsonConvert.DeserializeObject<DidRecord>(identityJson);
            if (!IsVerkey(record.Verkey))
            {
                throw new ArgumentException("Not a valid did: " + record.Did);
            }

            record.Verkey = await BuildFullVerkey(record.Did, record.Verkey);
            if (string.IsNullOrEmpty(record.Id))
            {
                record.Id = record.Did;
            }
            return record;
        }

        private static async Task Upsert(IWalletRecordService recordService, AriesStorage storage, DidRecord didRecord)
        {
            DidRecord existingRecord = await recordService.GetAsync<DidRecord>(storage, didRecord.Did);
            if (existingRecord != null)
            {
                await recordService.UpdateAsync(storage, didRecord);
            }
            else
            {
                await recordService.AddAsync(storage, didRecord);
            }
        }

        /// <summary>
        /// Gets the verification key for the specified DID.
        /// </summary>
        /// <remarks>
        /// If the provided agent context <cref name="storage"/> of the agent context does not contain the verification key associated with the specified DID then 
        /// an attempt will be made to look up the key from the provided agent context <cref name="Pool"/>. If resolved from the agent context <cref name="pool"/>
        /// then the DID and key will be automatically cached in the <cref name="wallet"/>.
        /// <note type="note">
        /// The <see cref="CreateAndStoreMyDidAsync(AriesStorage,IWalletRecordService, string, string,string,bool)"/> and <see cref="Crypto.CreateKeyAsync(Wallet, string)"/> methods both create
        /// similar wallet records so the returned verification key in all generic crypto and messaging functions.
        /// </note>
        /// </remarks>
        /// <param name="agentContext"></param>
        /// <param name="recordService"></param>
        /// <param name="ledgerService"></param>
        /// <param name="did">The DID to get the verification key for.</param>
        /// <returns>An asynchronous <see cref="Task{T}"/> that resolves to a string containing the verification key associated with the DID.</returns>
        /// <exception cref="WalletItemNotFoundException">Thrown if the DID could not be resolved from the <cref name="wallet"/> and <cref name="pool"/>.</exception>
        public static async Task<string> KeyForDidAsync(IAgentContext agentContext, IWalletRecordService recordService, ILedgerService ledgerService, string did)
        {
            string result;
            AriesStorage storage = agentContext.AriesStorage;
            DidRecord didRecord = await recordService.GetAsync<DidRecord>(storage, did);
            result = didRecord?.Verkey;

            if (string.IsNullOrEmpty(result))
            {
                string nymJson = await ledgerService.LookupNymAsync(agentContext, did);
                string data = JObject.Parse(nymJson)["result"]?["data"]?.ToString();
                string verkey = JObject.Parse(data)["verkey"]?.ToString();
                result = await BuildFullVerkey(did, verkey);
            }

            return result;
        }

        /// <summary>
        /// Retrieves abbreviated verkey if it is possible otherwise return full verkey.
        /// </summary>
        /// <returns>The verkey async.</returns>
        /// <param name="did">Did.</param>
        /// <param name="verKey">Full verkey.</param>
        public static Task<string> AbbreviateVerkeyAsync(string did, string verKey)
        {
            string decodedDid = Multibase.Base58.Decode(did).ToString();
            string decodedVerKey = Multibase.Base58.Decode(verKey).ToString();
            string firstPart = decodedVerKey[..16];
            string secondPart = decodedVerKey[17..^(-16)];

            return decodedDid.Equals(firstPart) ? Task.FromResult($"~{secondPart}") : Task.FromResult(verKey);
        }
        public static Task<bool> ValidateVerkeyED25519(string myVerkey)
        {
            string vk_abbrev;
            if (myVerkey.Contains(':'))
            {
                string[] splits = myVerkey.Split(':');
                string vk_part1 = splits[0];
                string cryptoType = splits[1];
                // For now only ED25519 supported as standard, which is indicated by verkeys without a ':cryptoType' addition
                if (!string.IsNullOrEmpty(cryptoType)) 
                    throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, $"Invalid key alg provided: '{cryptoType}', only key alg 'ED25519' supported for now and this algorithm is not contained in verkey.");
            }

            if (myVerkey.StartsWith("~"))
            {
                vk_abbrev = myVerkey.Remove(0,1);
                return Task.FromResult(IsVerkey(vk_abbrev));
            }

            return Task.FromResult(IsVerkey(myVerkey));
           
        }

        private static Task<string> BuildFullVerkey(string dest, string str)
        {
            string cryptoType = "";
            string verkey;
            if (str.Contains(':'))
            {
                string[] splits = str.Split(':');
                verkey = splits[0];
                cryptoType = splits[1];
            }
            else
            {
                verkey = str;
            }

            if (verkey.StartsWith("~"))
            {
                Multibase.Base58.Decode(dest).ToList<byte>().AddRange(Multibase.Base58.Decode(verkey[1..]).ToList<byte>());
            }

            if (!string.IsNullOrEmpty(cryptoType))
            {
                verkey = $"{verkey}:{cryptoType}";
            }

            return Task.FromResult(verkey);
        }

        public static async Task<string> KeyForLocalDidAsync(AriesStorage storage, IWalletRecordService recordService, string did)
        {
            if ((storage?.Wallet != null && storage?.Store != null) || (storage?.Wallet == null && storage?.Store == null))
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"Storage.Wallet is {storage?.Wallet} and Storage.Store is {storage?.Store}");
            }
            else if (storage?.Store != null)
            {
                DidRecord didRecord = await recordService.GetAsync<DidRecord>(storage, did);
                return didRecord?.Verkey;
            }
            else
            {
                return await Did.KeyForLocalDidAsync(storage.Wallet, did);
            }
        }
    }
}
