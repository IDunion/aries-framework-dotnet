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

namespace Hyperledger.Aries.Tests
{
    [Trait("Category", "DefaultV1")]
    public class ProvisioningTestsV1
    {
        private WalletConfiguration _config = new WalletConfiguration { Id = Guid.NewGuid().ToString() };
        private WalletCredentials _creds = new WalletCredentials { Key = "1" };

        private IWalletService _walletService;
        private IProvisioningService _provisioningService;

        public ProvisioningTestsV1()
        {
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
            var walletService = new DefaultWalletService();
            var provisioningService = new DefaultProvisioningService(
                new DefaultWalletRecordService(),
                walletService,
                Options.Create(new AgentOptions
                {
                    WalletConfiguration = _config,
                    WalletCredentials = _creds
                }));

            await provisioningService.ProvisionAgentAsync();

            var wallet = await walletService.GetWalletAsync(_config, _creds);
            Assert.NotNull(wallet);

            var provisioning = await provisioningService.GetProvisioningAsync(wallet);

            Assert.NotNull(provisioning);
            Assert.Null(provisioning.Endpoint.Uri);
        }

        [Fact]
        public async Task ProvisionNewWalletCanUpdateEndpoint()
        {
            var walletService = new DefaultWalletService();
            var provisioningService = new DefaultProvisioningService(
                new DefaultWalletRecordService(),
                walletService,
                Options.Create(new AgentOptions
                {
                    WalletConfiguration = _config,
                    WalletCredentials = _creds
                }));

            await provisioningService.ProvisionAgentAsync();

            var wallet = await walletService.GetWalletAsync(_config, _creds);
            Assert.NotNull(wallet);

            var provisioning = await provisioningService.GetProvisioningAsync(wallet);

            Assert.NotNull(provisioning);
            Assert.Null(provisioning.Endpoint.Uri);

            await provisioningService.UpdateEndpointAsync(wallet, new AgentEndpoint
            {
                Uri = "http://mock"
            });

            provisioning = await provisioningService.GetProvisioningAsync(wallet);

            Assert.NotNull(provisioning);
            Assert.NotNull(provisioning.Endpoint);
            Assert.NotNull(provisioning.Endpoint.Uri);
        }
    }

    [Trait("Category", "DefaultV2")]
    public class ProvisioningTestsV2
    {
        private WalletConfiguration _config = new WalletConfiguration { StorageConfiguration = new WalletConfiguration.WalletStorageConfiguration() { } };
        private WalletCredentials _creds = new WalletCredentials { Key = "1" };

        private IWalletService _walletService;
        private IProvisioningService _provisioningService;

        public ProvisioningTestsV2()
        {
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
            var walletService = new DefaultWalletService();
            var provisioningService = new DefaultProvisioningService(
                new DefaultWalletRecordService(),
                walletService,
                Options.Create(new AgentOptions
                {
                    WalletConfiguration = _config,
                    WalletCredentials = _creds
                }));

            await provisioningService.ProvisionAgentAsync();

            var wallet = await walletService.GetWalletAsync(_config, _creds);
            Assert.NotNull(wallet);

            var provisioning = await provisioningService.GetProvisioningAsync(wallet);

            Assert.NotNull(provisioning);
            Assert.Null(provisioning.Endpoint.Uri);
        }

        [Fact]
        public async Task ProvisionNewWalletCanUpdateEndpoint()
        {
            var walletService = new DefaultWalletService();
            var provisioningService = new DefaultProvisioningService(
                new DefaultWalletRecordService(),
                walletService,
                Options.Create(new AgentOptions
                {
                    WalletConfiguration = _config,
                    WalletCredentials = _creds
                }));

            await provisioningService.ProvisionAgentAsync();

            var wallet = await walletService.GetWalletAsync(_config, _creds);
            Assert.NotNull(wallet);

            var provisioning = await provisioningService.GetProvisioningAsync(wallet);

            Assert.NotNull(provisioning);
            Assert.Null(provisioning.Endpoint.Uri);

            await provisioningService.UpdateEndpointAsync(wallet, new AgentEndpoint
            {
                Uri = "http://mock"
            });

            provisioning = await provisioningService.GetProvisioningAsync(wallet);

            Assert.NotNull(provisioning);
            Assert.NotNull(provisioning.Endpoint);
            Assert.NotNull(provisioning.Endpoint.Uri);
        }
    }
}
