using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Ledger.Models;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Aries.Utils;
using Microsoft.Extensions.Options;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Configuration
{
    /// <inheritdoc />
    public class DefaultProvisioningServiceV2 : IProvisioningService
    {
        /// <summary>The record service</summary>
        // ReSharper disable InconsistentNaming
        protected readonly IWalletRecordService RecordService;

        /// <summary>The wallet service</summary>
        protected readonly IWalletService WalletService;
        /// <summary>
        /// Agent options
        /// </summary>
        protected readonly AgentOptions AgentOptions;

        // ReSharper restore InconsistentNaming

        /// <summary>Initializes a new instance of the <see cref="DefaultProvisioningService"/> class.</summary>
        /// <param name="walletRecord">The wallet record.</param>
        /// <param name="walletService">The wallet service.</param>
        /// <param name="agentOptions">Configured agent options.</param>
        public DefaultProvisioningServiceV2(
            IWalletRecordService walletRecord,
            IWalletService walletService,
            IOptions<AgentOptions> agentOptions)
        {
            RecordService = walletRecord;
            WalletService = walletService;
            AgentOptions = agentOptions.Value;
        }

        /// <inheritdoc />
        public async Task AcceptTxnAuthorAgreementAsync(IAgentContext agentContext, IndyTaa txnAuthorAgreement, string acceptanceMechanism = "service_agreement")
        {
            ProvisioningRecord provisioning = await GetProvisioningAsync(agentContext.AriesStorage);

            provisioning.TaaAcceptance = new IndyTaaAcceptance
            {
                Digest = GetDigest(txnAuthorAgreement),
                Text = txnAuthorAgreement.Text,
                Version = txnAuthorAgreement.Version,
                AcceptanceDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                AcceptanceMechanism = acceptanceMechanism
            };

            await RecordService.UpdateAsync(agentContext.AriesStorage, provisioning);
        }

        private string GetDigest(IndyTaa taa)
        {
            using SHA256 shaAlgorithm = SHA256.Create();
            return shaAlgorithm.ComputeHash(
                $"{taa.Version}{taa.Text}"
                .GetUTF8Bytes())
            .ToHexString();
        }

        /// <inheritdoc />
        public virtual async Task<ProvisioningRecord> GetProvisioningAsync(AriesStorage storage)
        {
            ProvisioningRecord record = await RecordService.GetAsync<ProvisioningRecord>(storage, ProvisioningRecord.UniqueRecordId);

            return record ?? throw new AriesFrameworkException(ErrorCode.RecordNotFound, "Provisioning record not found");
        }

        /// <inheritdoc />
        public virtual async Task UpdateEndpointAsync(AriesStorage storage, AgentEndpoint endpoint)
        {
            ProvisioningRecord record = await GetProvisioningAsync(storage);
            record.Endpoint = endpoint;

            await RecordService.UpdateAsync(storage, record);
        }

        /// <inheritdoc />
        public virtual Task ProvisionAgentAsync()
        {
            return ProvisionAgentAsync(AgentOptions);
        }

        /// <inheritdoc />
        public virtual async Task ProvisionAgentAsync(AgentOptions agentOptions)
        {
            if (agentOptions is null)
            {
                throw new ArgumentNullException(nameof(agentOptions));
            }

            // Create agent wallet
            await WalletService.CreateWalletAsync(
                configuration: agentOptions.WalletConfiguration,
                credentials: agentOptions.WalletCredentials);
            AriesStorage storage = await WalletService.GetWalletAsync(
                configuration: agentOptions.WalletConfiguration,
                credentials: agentOptions.WalletCredentials);

            // Configure agent endpoint
            AgentEndpoint endpoint = null;
            if (agentOptions.EndpointUri != null)
            {
                endpoint = new AgentEndpoint { Uri = agentOptions.EndpointUri.ToString() };
                if (agentOptions.AgentKeySeed != null)
                {
                    (string did, string verKey) = await DidUtils.CreateAndStoreMyDidAsync(storage, RecordService, seed: agentOptions.AgentKeySeed);
                    endpoint.Did = did;
                    endpoint.Verkey = new[] { verKey };
                }
                else if (agentOptions.AgentKey != null)
                {
                    endpoint.Did = agentOptions.AgentDid;
                    endpoint.Verkey = new[] { agentOptions.AgentKey };
                }
                else
                {
                    (string did, string verKey) = await DidUtils.CreateAndStoreMyDidAsync(storage, RecordService);
                    endpoint.Did = did;
                    endpoint.Verkey = new[] { verKey };
                }
            }

            //TODO : ??? - maybe better solution : add masterSecretJson as Tag to provisioningRecord instead of creating a new MasterSecretRecord?
            //string masterSecretId = Guid.NewGuid().ToString();
            //record.SetTag(TagConstants.MasterSecretJson, await indy_shared_rs_dotnet.IndyCredx.MasterSecretApi.CreateMasterSecretJsonAsync());
            string masterSecretId = await MasterSecretUtils.CreateAndStoreMasterSecretAsync(storage: storage, recordService: RecordService);

            ProvisioningRecord record = new()
            {
                MasterSecretId = masterSecretId,
                Endpoint = endpoint,
                Owner =
                {
                    Name = agentOptions.AgentName,
                    ImageUrl = agentOptions.AgentImageUri
                }
            };

            // Issuer Configuration
            agentOptions.IssuerKeySeed ??= CryptoUtils.GetUniqueKey(32);

            (string issuerDid, string issuerVerKey) = await DidUtils.CreateAndStoreMyDidAsync(
                storage,
                RecordService,
                did: agentOptions.IssuerDid,
                seed: agentOptions.IssuerKeySeed);

            record.IssuerSeed = agentOptions.IssuerKeySeed;
            record.IssuerDid = issuerDid;
            record.IssuerVerkey = issuerVerKey;
            record.TailsBaseUri = agentOptions.EndpointUri != null
                ? new Uri(new Uri(agentOptions.EndpointUri), "tails/").ToString()
                : null;

            record.UseMessageTypesHttps = agentOptions.UseMessageTypesHttps;

            record.SetTag("AgentKeySeed", agentOptions.AgentKeySeed);
            record.SetTag("IssuerKeySeed", agentOptions.IssuerKeySeed);

            // Add record to wallet
            await RecordService.AddAsync(storage, record);
        }
    }
}
