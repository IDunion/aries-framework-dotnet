using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using aries_askar_dotnet.Models;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Ledger;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Utils;
using Microsoft.Extensions.Options;

namespace Hyperledger.Aries.Configuration
{
    /// <inheritdoc />
    public class NewProvisioningService : INewProvisioningService
    {
        /// <summary>The record service</summary>
        // ReSharper disable InconsistentNaming
        protected readonly INewWalletRecordService RecordService;

        /// <summary>The wallet service</summary>
        protected readonly INewWalletService WalletService;
        /// <summary>
        /// Agent options
        /// </summary>
        protected readonly AgentOptions AgentOptions;

        // ReSharper restore InconsistentNaming

        /// <summary>Initializes a new instance of the <see cref="DefaultProvisioningService"/> class.</summary>
        /// <param name="walletRecord">The wallet record.</param>
        /// <param name="walletService">The wallet service.</param>
        /// <param name="agentOptions"></param>
        public NewProvisioningService(
            INewWalletRecordService walletRecord,
            INewWalletService walletService,
            IOptions<AgentOptions> agentOptions)
        {
            RecordService = walletRecord;
            WalletService = walletService;
            AgentOptions = agentOptions.Value;
        }

        /// <inheritdoc />
        public async Task AcceptTxnAuthorAgreementAsync(IAgentContext agentContext, IndyTaa txnAuthorAgreement, string acceptanceMechanism = "service_agreement")
        {
            var provisioning = await GetProvisioningAsync(agentContext.WalletStore);

            provisioning.TaaAcceptance = new IndyTaaAcceptance
            {
                Digest = GetDigest(txnAuthorAgreement),
                Text = txnAuthorAgreement.Text,
                Version = txnAuthorAgreement.Version,
                AcceptanceDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                AcceptanceMechanism = acceptanceMechanism
            };

            await RecordService.UpdateAsync(agentContext.WalletStore, provisioning);
        }

        private string GetDigest(IndyTaa taa)
        {
            using (var shaAlgorithm = SHA256.Create())
                return shaAlgorithm.ComputeHash(
                    $"{taa.Version}{taa.Text}"
                    .GetUTF8Bytes())
                .ToHexString();
        }

        /// <inheritdoc />
        public virtual async Task<ProvisioningRecord> GetProvisioningAsync(Store wallet)
        {
            var record = await RecordService.GetAsync<ProvisioningRecord>(wallet, ProvisioningRecord.UniqueRecordId);

            if (record == null)
                throw new AriesFrameworkException(ErrorCode.RecordNotFound, "Provisioning record not found");

            return record;
        }

        /// <inheritdoc />
        public virtual async Task UpdateEndpointAsync(Store wallet, AgentEndpoint endpoint)
        {
            var record = await GetProvisioningAsync(wallet);
            record.Endpoint = endpoint;

            await RecordService.UpdateAsync(wallet, record);
        }

        /// <inheritdoc />
        public virtual Task ProvisionAgentAsync() => ProvisionAgentAsync(AgentOptions);

        /// <inheritdoc />
        public async virtual Task ProvisionAgentAsync(AgentOptions agentOptions)
        {
            if (agentOptions is null)
            {
                throw new ArgumentNullException(nameof(agentOptions));
            }

            // Create agent wallet
            await WalletService.CreateWalletAsync(
                configuration: agentOptions.WalletConfiguration,
                credentials: agentOptions.WalletCredentials);
            var wallet = await WalletService.GetWalletAsync(
                configuration: agentOptions.WalletConfiguration,
                credentials: agentOptions.WalletCredentials);

            // Configure agent endpoint
            AgentEndpoint endpoint = null;
            if (agentOptions.EndpointUri != null)
            {
                endpoint = new AgentEndpoint { Uri = agentOptions.EndpointUri.ToString() };
                if (agentOptions.AgentKeySeed != null)
                {
                    var (did, verKey) = await DidUtils.CreateAndStoreMyDidAsync(wallet, seed: agentOptions.AgentKeySeed);
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
                    var (did, verKey) = await DidUtils.CreateAndStoreMyDidAsync(wallet);
                    endpoint.Did = did;
                    endpoint.Verkey = new[] { verKey };
                }
            }

            string masterSecretId  = await MasterSecretUtils.CreateAndStoreMasterSecretAsync(wallet: wallet , recordService : RecordService);

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
            if (agentOptions.IssuerKeySeed == null)
            {
                agentOptions.IssuerKeySeed = CryptoUtils.GetUniqueKey(32);
            }

            var (issuerDid, issuerVerKey) = await DidUtils.CreateAndStoreMyDidAsync(
                wallet, 
                did : agentOptions.IssuerDid, 
                seed : agentOptions.IssuerKeySeed);

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
            await RecordService.AddAsync(wallet, record);
        }
    }
}
