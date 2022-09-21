using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Ledger.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Configuration
{
    /// <summary>
    /// Transaction author acceptance service registered as hosted service on startup
    /// to check if acceptance is required and accept if needed.
    /// The acceptance trail (digest and timestamp) is saved in the <see cref="ProvisioningRecord" />
    /// </summary>
    public class TxnAuthorAcceptanceService : IHostedService
    {
        private readonly IProvisioningService _provisioningService;
        private readonly IPoolService _poolService;
        private readonly IAgentProvider _agentProvider;
        private readonly AgentOptions _agentOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="TxnAuthorAcceptanceService" /> class.
        /// </summary>
        /// <param name="hostApplicationLifetime"></param>
        /// <param name="provisioningService"></param>
        /// <param name="poolService"></param>
        /// <param name="agentProvider"></param>
        /// <param name="agentOptions"></param>
        public TxnAuthorAcceptanceService(
            IHostApplicationLifetime hostApplicationLifetime,
            IProvisioningService provisioningService,
            IPoolService poolService,
            IAgentProvider agentProvider,
            IOptions<AgentOptions> agentOptions)
        {
            _ = hostApplicationLifetime.ApplicationStarted.Register(AcceptTxnAuthorAgreement);
            _provisioningService = provisioningService;
            _poolService = poolService;
            _agentProvider = agentProvider;
            _agentOptions = agentOptions.Value;
        }

        private async void AcceptTxnAuthorAgreement()
        {
            IAgentContext context = await _agentProvider.GetContextAsync(nameof(TxnAuthorAcceptanceService));
            IndyTaa taa = await _poolService.GetTaaAsync(_agentOptions.PoolName);
            if (taa != null)
            {
                string digest = GetDigest(taa);
                ProvisioningRecord provisioning = await _provisioningService.GetProvisioningAsync(context.AriesStorage);

                if (provisioning.TaaAcceptance == null || provisioning.TaaAcceptance.Digest != digest)
                {
                    await _provisioningService.AcceptTxnAuthorAgreementAsync(context, taa);
                }
            }
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private string GetDigest(IndyTaa taa)
        {
            using SHA256 shaAlgorithm = SHA256.Create();
            return shaAlgorithm.ComputeHash(
                $"{taa.Version}{taa.Text}"
                .GetUTF8Bytes())
            .ToHexString();
        }
    }
}
