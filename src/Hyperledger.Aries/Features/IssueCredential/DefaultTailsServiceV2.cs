using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.PresentProof;
using Hyperledger.Aries.Utils;
using Hyperledger.Indy.BlobStorageApi;
using Hyperledger.Indy.PoolApi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multiformats.Base;
using Newtonsoft.Json.Linq;

namespace Hyperledger.Aries.Features.IssueCredential
{
    /// <inheritdoc />
    public class DefaultTailsServiceV2 : ITailsService
    {
        /// <summary>The BLOB readers</summary>
        protected static readonly ConcurrentDictionary<string, BlobStorageReader> BlobReaders =
            new ConcurrentDictionary<string, BlobStorageReader>();

        /// <summary>The ledger service</summary>
        // ReSharper disable InconsistentNaming
        protected readonly ILedgerService LedgerService;
        /// <summary>
        /// The agent options
        /// </summary>
        protected readonly AgentOptions AgentOptions;

        /// <summary>The HTTP client</summary>
        protected readonly HttpClient HttpClient;
        // ReSharper restore InconsistentNaming

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultTailsServiceV2" /> class.
        /// </summary>
        /// <param name="ledgerService">The ledger service.</param>
        /// <param name="agentOptions">The agent options.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        public DefaultTailsServiceV2(
            ILedgerService ledgerService,
            IOptions<AgentOptions> agentOptions,
            IHttpClientFactory httpClientFactory)
        {
            LedgerService = ledgerService;
            AgentOptions = agentOptions.Value;
            HttpClient = httpClientFactory.CreateClient();
        }

        public Task<BlobStorageWriter> CreateTailsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<BlobStorageReader> OpenTailsAsync(string filename)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public virtual async Task<string> EnsureTailsExistsAsync(IAgentContext agentContext, string revocationRegistryId)
        {
            Debug.WriteLine($"Called {nameof(EnsureTailsExistsAsync)}'");

            var revocationRegistry = await LedgerService.LookupRevocationRegistryDefinitionAsync(agentContext, revocationRegistryId);
            var tailsUri = JObject.Parse(revocationRegistry.ObjectJson)["value"]["tailsLocation"].ToObject<string>();
            var tailsFileName = JObject.Parse(revocationRegistry.ObjectJson)["value"]["tailsHash"].ToObject<string>();

            Debug.WriteLine($"Aries method - EnsureTailsExistsAsync() - tailsUri: '{tailsUri}'.");
            Debug.WriteLine($"Aries method - EnsureTailsExistsAsync() - tailsFileName: '{tailsFileName}'.");

            var tailsfile = Path.Combine(AgentOptions.RevocationRegistryDirectory, tailsFileName);

            Debug.WriteLine($"Aries method - EnsureTailsExistsAsync() - tailsfile: '{tailsfile}'.");

            var hash = Multibase.Base58.Decode(tailsFileName);

            if (!Directory.Exists(AgentOptions.RevocationRegistryDirectory))
            {
                Debug.WriteLine($"Aries method - EnsureTailsExistsAsync() - Directory not existing '{AgentOptions.RevocationRegistryDirectory}'. Directory getting created...");
                Directory.CreateDirectory(AgentOptions.RevocationRegistryDirectory);
                Debug.WriteLine($"Aries method - EnsureTailsExistsAsync() - Directory was created '{AgentOptions.RevocationRegistryDirectory}'.");
            }
            else
            {
                Debug.WriteLine($"Aries method - EnsureTailsExistsAsync() - Directory already exists '{AgentOptions.RevocationRegistryDirectory}'.");
            }

            try
            {
                Debug.WriteLine($"Aries method - EnsureTailsExistsAsync() - Tailsfile already saved on device ... ?");
                if (!File.Exists(tailsfile))
                {
                    Debug.WriteLine($"Aries method - EnsureTailsExistsAsync() - No");
                    var bytes = await HttpClient.GetByteArrayAsync(new Uri(tailsUri));

                    // Check hash
                    using var sha256 = SHA256.Create();
                    var computedHash = sha256.ComputeHash(bytes);

                    if (!computedHash.SequenceEqual(hash))
                    {
                        throw new Exception("Tails file hash didn't match");
                    }

                    File.WriteAllBytes(
                        path: tailsfile,
                        bytes: bytes);

                    Debug.WriteLine($"Aries method - EnsureTailsExistsAsync() - Tailsfile was saved on device?");
                }
                else
                {
                    Debug.WriteLine($"Aries method - EnsureTailsExistsAsync() - Yes");
                }
            }
            catch (Exception e)
            {
                throw new AriesFrameworkException(
                    errorCode: ErrorCode.RevocationRegistryUnavailable,
                    message: $"Unable to retrieve revocation registry from the specified URL '{tailsUri}'. Error: {e.Message}");
            }

            return tailsfile;
        }
    }
}
