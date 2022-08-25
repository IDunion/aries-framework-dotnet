using Hyperledger.Aries.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Configuration
{
    internal class PoolConfigurationServiceV2 : IHostedService
    {
        private readonly AgentOptions _agentOptions;
        private readonly IPoolService _poolService;
        private readonly ILogger<PoolConfigurationServiceV2> _logger;

        public PoolConfigurationServiceV2(
            IOptions<AgentOptions> options,
            IPoolService poolService,
            ILogger<PoolConfigurationServiceV2> logger)
        {
            _agentOptions = options.Value;
            _poolService = poolService;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_agentOptions.GenesisFilename == null)
                {
                    _logger.LogWarning("Pool configuration genesis file not supplied.");
                    return;
                }
                await _poolService.CreatePoolAsync(_agentOptions.PoolName, _agentOptions.GenesisFilename);
            }
            /* TODO: ??? Which Wrapper is thrown when ledger config already exists? */
            //catch (PoolLedgerConfigExistsException)
            //{
            //    // Pool already exists, swallow exception
            //}
            catch (Exception e)
            {
                _logger.LogCritical(e, "Couldn't create ledger configuration");
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
