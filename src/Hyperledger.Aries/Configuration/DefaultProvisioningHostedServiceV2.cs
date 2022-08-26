using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Configuration
{
    internal class DefaultProvisioningHostedServiceV2 : IHostedService
    {
        private readonly IProvisioningService _provisioningService;

        public DefaultProvisioningHostedServiceV2(IProvisioningService provisioningService)
        {
            _provisioningService = provisioningService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _provisioningService.ProvisionAgentAsync();
            }
            catch (Exception)
            {
                // OK
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
