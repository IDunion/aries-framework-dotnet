using System;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Options;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Storage;
using static Hyperledger.Aries.Tests.DidUtilsTests;
using Hyperledger.Indy.WalletApi;
using Hyperledger.TestHarness.Utils;
using Hyperledger.TestHarness;
using Microsoft.Extensions.Hosting;
using Hyperledger.Indy.PoolApi;
using Microsoft.Extensions.DependencyInjection;

namespace Hyperledger.Aries.Tests
{
    public abstract class ProvisioningServiceTests
    {
        protected WalletConfiguration _config;
        protected WalletCredentials _creds;

        protected IWalletService _walletService;
        protected IProvisioningService _provisioningService;

        [Fact]
        public async Task ProvisionNewWalletWithEndpoint()
        {
            await _provisioningService.ProvisionAgentAsync(
                new AgentOptions
                {
                    EndpointUri = "http://mock",
                    WalletConfiguration = _config,
                    WalletCredentials = _creds
                });

            var wallet = await _walletService.GetWalletAsync(_config, _creds);
            Assert.NotNull(wallet);

            var provisioning = await _provisioningService.GetProvisioningAsync(wallet);

            Assert.NotNull(provisioning);
            Assert.NotNull(provisioning.Endpoint);
            Assert.NotNull(provisioning.Endpoint.Did);
            Assert.NotNull(provisioning.Endpoint.Verkey);
        }

        [Fact]
        public async Task ProvisionNewWalletWithoutEndpoint()
        {
            await _provisioningService.ProvisionAgentAsync();

            var wallet = await _walletService.GetWalletAsync(_config, _creds);
            Assert.NotNull(wallet);

            var provisioning = await _provisioningService.GetProvisioningAsync(wallet);

            Assert.NotNull(provisioning);
            Assert.Null(provisioning.Endpoint.Uri);
        }

        [Fact]
        public async Task ProvisionNewWalletCanUpdateEndpoint()
        {
            await _provisioningService.ProvisionAgentAsync();

            var wallet = await _walletService.GetWalletAsync(_config, _creds);
            Assert.NotNull(wallet);

            var provisioning = await _provisioningService.GetProvisioningAsync(wallet);

            Assert.NotNull(provisioning);
            Assert.Null(provisioning.Endpoint.Uri);

            await _provisioningService.UpdateEndpointAsync(wallet, new AgentEndpoint
            {
                Uri = "http://mock"
            });

            provisioning = await _provisioningService.GetProvisioningAsync(wallet);

            Assert.NotNull(provisioning);
            Assert.NotNull(provisioning.Endpoint);
            Assert.NotNull(provisioning.Endpoint.Uri);
        }
    }

    [Trait("Category", "DefaultV1")]
    public class ProvisioningServiceTestsV1 : ProvisioningServiceTests, IAsyncLifetime
    {
        public async Task DisposeAsync()
        {
            await _walletService.DeleteWalletAsync(_config, _creds);
        }

        public async Task InitializeAsync()
        {
            _config = new WalletConfiguration { Id = Guid.NewGuid().ToString() };
            _creds = new WalletCredentials { Key = "1" };
            _walletService = new DefaultWalletService();
            _provisioningService = new DefaultProvisioningService(
                new DefaultWalletRecordService(),
                _walletService,
                Options.Create(new AgentOptions
                {
                    WalletConfiguration = _config,
                    WalletCredentials = _creds
                }));
        }
    }

    [Trait("Category", "DefaultV2")]
    public class ProvisioningServiceTestsV2 : ProvisioningServiceTests, IAsyncLifetime
    {
        public async Task DisposeAsync()
        {
            await _walletService.DeleteWalletAsync(_config, _creds);
        }

        public async Task InitializeAsync()
        {
            _config = TestConstants.TestSingleWalletV2WalletConfig;
            _creds = TestConstants.TestSingelWalletV2WalletCreds;
            _walletService = new DefaultWalletServiceV2();
            _provisioningService = new DefaultProvisioningServiceV2(
                new DefaultWalletRecordServiceV2(),
                _walletService,
                Options.Create(new AgentOptions
                {
                    WalletConfiguration = _config,
                    WalletCredentials = _creds
                }));
        }
    }
}
