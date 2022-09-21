using Castle.Core.Logging;
using FluentAssertions;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Ledger;
using Hyperledger.Aries.Storage;
using Hyperledger.TestHarness;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Hyperledger.Aries.Tests
{
    [Trait("Category", "DefaultV2")]
    public class PoolConfigurationServiceTestsV2 : IAsyncLifetime
    {
        private PoolConfigurationServiceV2 _poolConfigurationService;
        IOptions<AgentOptions> _options;

        public async Task InitializeAsync()
        {
            _options = Options.Create<AgentOptions>(new AgentOptions
            {
                WalletConfiguration = TestConstants.TestSingleWalletV2WalletConfig,
                WalletCredentials = TestConstants.TestSingelWalletV2WalletCreds,
                EndpointUri = TestConstants.DefaultMockUri
            });
            IPoolService poolService = new DefaultPoolServiceV2();

            _poolConfigurationService = new(_options, poolService, new Mock<ILogger<PoolConfigurationServiceV2>>().Object);
        }

        [Fact(DisplayName = "Simply returns nothing when no Genesisfile is set.")]
        public async Task StartReturnsWithoutGenesisFile()
        {
            //Arrange

            //Act
            var act = async () => await _poolConfigurationService.StartAsync(new CancellationTokenSource().Token);

            //Assert
            await act.Should().NotThrowAsync<Exception>();
        }

        [Fact(DisplayName = "Creates a new Pool when genesis file and name is set and no pool exists.")]
        public async Task StartCreatesANewPool()
        {
            //Arrange
            _options.Value.GenesisFilename = Path.GetFullPath("pool_genesis.txn");
            _options.Value.PoolName = "TestPool";

            //Act
            var act = async () => await _poolConfigurationService.StartAsync(new CancellationTokenSource().Token);

            //Assert
            await act.Should().NotThrowAsync<Exception>();
        }

        [Fact(DisplayName = "Does nothing when pool already exists.")]
        public async Task IgnoreExistingPool()
        {
            //Arrange
            _options.Value.GenesisFilename = Path.GetFullPath("pool_genesis.txn");
            _options.Value.PoolName = "TestPool";
            DefaultPoolServiceV2 poolService = new();
            await poolService.CreatePoolAsync(_options.Value.PoolName, _options.Value.GenesisFilename);

            //Act
            var act = async () => await _poolConfigurationService.StartAsync(new CancellationTokenSource().Token);

            //Assert
            await act.Should().NotThrowAsync<Exception>();
        }

        [Fact(DisplayName = "Throws Exception when ledger configuration is invalid.")]
        public async Task ThrowsExceptionForInvalidConfig()
        {
            //Arrange
            _options.Value.GenesisFilename = Path.GetFullPath("pool_genesis_invalid.txn");
            _options.Value.PoolName = "blubb";

            //Act
            var act = async () => await _poolConfigurationService.StartAsync(new CancellationTokenSource().Token);

            //Assert
            await act.Should().ThrowAsync<Exception>();
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
        
    }
}
