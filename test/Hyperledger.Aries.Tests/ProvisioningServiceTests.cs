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
using Hyperledger.Aries.Ledger.Models;
using FluentAssertions;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Features.Handshakes.DidExchange;

namespace Hyperledger.Aries.Tests
{
    public abstract class ProvisioningServiceTests
    {
        protected WalletConfiguration _config;
        protected WalletCredentials _creds;

        protected IWalletService _walletService;
        protected IWalletRecordService _walletRecordService;
        protected IProvisioningService _provisioningService;
        protected IAgentContext _agentContext;

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
            _walletRecordService = new DefaultWalletRecordService();
            _provisioningService = new DefaultProvisioningService(
                _walletRecordService,
                _walletService,
                Options.Create(new AgentOptions
                {
                    WalletConfiguration = _config,
                    WalletCredentials = _creds
                }));
            
            _agentContext = await AgentUtils.Create($"{{\"id\":\"{Guid.NewGuid()}\"}}", "{\"key\":\"test_wallet_key\"}");
        }
    }

    [Trait("Category", "DefaultV2")]
    public class ProvisioningServiceTestsV2 : ProvisioningServiceTests, IAsyncLifetime
    {
        IOptions<AgentOptions> _agentOptions;

        public async Task DisposeAsync()
        {
            await _walletService.DeleteWalletAsync(_config, _creds);
        }

        public async Task InitializeAsync()
        {
            _config = TestConstants.TestSingleWalletV2WalletConfig;
            _creds = TestConstants.TestSingelWalletV2WalletCreds;
            _walletService = new DefaultWalletServiceV2();
            _walletRecordService = new DefaultWalletRecordServiceV2();
            _agentOptions = Options.Create(new AgentOptions
            {
                WalletConfiguration = _config,
                WalletCredentials = _creds
            });
            _provisioningService = new DefaultProvisioningServiceV2(
                _walletRecordService,
                _walletService,
                _agentOptions);
            _agentContext = await AgentUtils.CreateV2(_walletService, TestConstants.TestSingleWalletV2WalletConfig, TestConstants.TestSingelWalletV2WalletCreds);
            await Task.FromResult(0);
        }

        [Fact]
        public async Task AcceptTxnAuthorAgreementCreatesAcceptance()
        {
            ProvisioningRecord record = new ProvisioningRecord { Endpoint = new AgentEndpoint { Uri = "https://mock.com" } };
            await _walletRecordService.AddAsync<ProvisioningRecord>(_agentContext.AriesStorage, record);
            string taaText = "testText";
            IndyTaa agreement = new IndyTaa { Text = taaText };

            await _provisioningService.AcceptTxnAuthorAgreementAsync(_agentContext, agreement);

            ProvisioningRecord updatedRecord = await _provisioningService.GetProvisioningAsync(_agentContext.AriesStorage);
            updatedRecord.TaaAcceptance.Should().NotBe(null);
            updatedRecord.TaaAcceptance.Text.Should().Be(taaText);
        }

        [Fact]
        public async Task AcceptTxnAuthorAgreementThrowsExceptionWhenAgreementNull()
        {
            var act = async () => await _provisioningService.AcceptTxnAuthorAgreementAsync(_agentContext, null);
            await act.Should().ThrowAsync<AriesFrameworkException>();
        }


        [Fact]
        public async Task ProvisioningNewWalletWithoutOptionsThrowsException()
        {
            var act = async () => await _provisioningService.ProvisionAgentAsync(null);
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task ProvisioningWithAgentKeySeedCreatesDidRecord()
        {
            _agentOptions.Value.AgentKeySeed = "blubbblubbblubbblubbblubbblubb--";
            _agentOptions.Value.EndpointUri = "https://mock.com";

             await _provisioningService.ProvisionAgentAsync();

            var wallet = await _walletService.GetWalletAsync(_config, _creds);
            var provisioning = await _provisioningService.GetProvisioningAsync(wallet);

            var didRecord = await _walletRecordService.GetAsync<DidRecord>(_agentContext.AriesStorage, provisioning.Endpoint.Did);
            didRecord.Should().NotBeNull();
        }
    }
}
