using aries_askar_dotnet.Models;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Features.Handshakes.DidExchange;
using Hyperledger.Aries.Storage;
using Multiformats.Base;
using Newtonsoft.Json;
using Stateless.Graph;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AriesAskarKey = aries_askar_dotnet.AriesAskar.KeyApi;
using AriesAskarStore = aries_askar_dotnet.AriesAskar.StoreApi;

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
            if (IsFullVerkey(verkey) == false)
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
            if (IsDidKey(didKey) == false)
            {
                throw new ArgumentException($"Value {didKey} is no did:key", nameof(didKey));
            }

            //string base58EncodedKey = didKey[$"{DIDKEY_PREFIX}:{BASE58_PREFIX}".Length..];
            string base58EncodedKey = "";
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
        /// <param name="wallet">The wallet.</param>
        /// <param name="did">The did as string; default null.</param>
        /// <param name="seed">The seed as string;default null.</param>
        /// <param name="cryptoType">The cryptoType as string; default "ed25519".</param>
        /// <param name="cid">The cid as bool; default false.</param>
        /// <returns>A tuple of strings. First is the did in format of "did":"method":"verkey". Second is the verkey </returns>
        /// <exception cref="AriesFrameworkException"></exception>
        public static async Task<(string, string)> CreateAndStoreMyDidAsync(
            Store wallet,
            IWalletRecordService recordService,
            string did = null,
            string seed = null,
            string cryptoType = "ed25519",
            bool cid = false)
        {
            if (wallet is null)
            {
                throw new ArgumentNullException(nameof(wallet));
            }

            KeyAlg keyAlg = cryptoType switch
            {   // only member currently supported is "ed25519"
                "ed25519" => KeyAlg.ED25519,
                _ => KeyAlg.ED25519,
            };

            if (string.IsNullOrEmpty(seed))
                seed = CryptoUtils.GetUniqueKey(32);

            IntPtr keyHandle = await AriesAskarKey.CreateKeyFromSeedAsync(
                keyAlg: keyAlg,
                seed: seed,
                SeedMethod.BlsKeyGen);

            var verKey = await AriesAskarKey.GetPublicBytesFromKeyAsync(keyHandle);
            string verKeyInDid;
            if (string.IsNullOrEmpty(did))
            {
                if (cid == true)
                {
                    verKeyInDid = Multibase.Base58.Encode(verKey);
                }
                else
                {
                    verKeyInDid = Multibase.Base58.Encode(verKey.Take(16).ToArray());
                }

                did = ToDid(DidKeyMethodSpec, verKeyInDid);
            }
            else
            {
                //Do nothing. did does not change only verKey and secretKey rotate
            }

            string verKeyBase58 = Multibase.Base58.Encode(verKey);
            if (cryptoType != "ed25519" && !string.IsNullOrEmpty(cryptoType))
                verKeyBase58 = verKeyBase58 + ":" + cryptoType;

            //TODO : ??? - add next lines to recordService method or a new "keyService" ?
            await recordService.AddKeyAsync(wallet, keyHandle, did);

            return (did, verKeyBase58);
        }

        //TODO : ??? - add missing functions?

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
        /// <param name="wallet">The wallet to store the DID in.</param>
        /// <param name="identityJson">The identity JSON.</param>
        /// <returns>An asynchronous <see cref="Task"/> that  with no return value the completes when the operation completes.</returns>
        public static async Task StoreTheirDidAsync(IWalletRecordService recordService, Store wallet, string identityJson)
        {
            if (wallet is null)
            {
                throw new ArgumentNullException(nameof(wallet));
            }

            if (string.IsNullOrEmpty(identityJson))
            {
                throw new ArgumentNullException(nameof(identityJson));
            }

            DidRecord theirDid = await CreateTheirDidAsync(identityJson);
            await Upsert(recordService, wallet, theirDid);
        }

        private static async Task<DidRecord> CreateTheirDidAsync(string identityJson)
        {
            DidRecord record = JsonConvert.DeserializeObject<DidRecord>(identityJson);
            if (!IsVerkey(record.Verkey))
            {
                throw new ArgumentException("Not a valid did: " + record.Did);
            }

            record.Verkey = await BuildFullVerkey(record.Did, record.Verkey);
            return record;
        }

        private static async Task Upsert(IWalletRecordService recordService, Store wallet, DidRecord didRecord)
        {
            DidRecord existingRecord =  await recordService.GetAsync<DidRecord>(wallet, didRecord.Did);
            if (existingRecord != null)
            {
                await recordService.UpdateAsync(wallet, didRecord);
            }
            else
            {
                await recordService.AddAsync(wallet, didRecord);
            }            
        }

        /// <summary>
        /// Gets the verification key for the specified DID.
        /// </summary>
        /// <remarks>
        /// If the provided <paramref name="wallet"/> of the agent context does not contain the verification key associated with the specified DID then 
        /// an attempt will be made to look up the key from the provided agent context <paramref name="pool"/>. If resolved from the agent context <paramref name="pool"/>
        /// then the DID and key will be automatically cached in the <paramref name="wallet"/>.
        /// <note type="note">
        /// The <see cref="CreateAndStoreMyDidAsync(Wallet, string)"/> and <see cref="Crypto.CreateKeyAsync(Wallet, string)"/> methods both create
        /// similar wallet records so the returned verification key in all generic crypto and messaging functions.
        /// </note>
        /// </remarks>
        /// <param name="agentContext"></param>
        /// <param name="did">The DID to get the verification key for.</param>
        /// <returns>An asynchronous <see cref="Task{T}"/> that resolves to a string containing the verification key associated with the DID.</returns>
        /// <exception cref="WalletItemNotFoundException">Thrown if the DID could not be resolved from the <paramref name="wallet"/> and <paramref name="pool"/>.</exception>
        public static async Task<string> KeyForDidAsync(IAgentContext agentContext, string did)
        {
            return "";
        }

        public static async Task<string> AbbreviateVerkeyAsync(string did, string verKey)
        {
            throw new NotImplementedException();
        }

        private static async Task<string> BuildFullVerkey(string dest, string str)
        {
            string verkey = "";
            string cryptoType = "";
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
                Multibase.Base58.Decode(dest).ToList<byte>().AddRange(Multibase.Base58.Decode(verkey.Substring(1, verkey.Length - 1)).ToList<byte>());
            }

            if (String.IsNullOrEmpty(cryptoType))
            {
                verkey = $"{verkey}:{cryptoType}";
            }

            return verkey;
        }
    }
}
