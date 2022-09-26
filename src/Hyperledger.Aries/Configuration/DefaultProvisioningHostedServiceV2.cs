using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Hyperledger.Aries.Configuration
{
    /// <inheritdoc />
    internal class DefaultProvisioningHostedServiceV2 : IHostedService
    {
        private readonly IProvisioningService _provisioningService;

        /// <inheritdoc />
        public DefaultProvisioningHostedServiceV2(IProvisioningService provisioningService)
        {
            _provisioningService = provisioningService;
        }

        /// <inheritdoc />
        public async virtual Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _provisioningService.ProvisionAgentAsync();
            }
            catch
            {
                // Ok
            }
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
