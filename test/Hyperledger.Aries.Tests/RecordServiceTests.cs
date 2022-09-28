using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using aries_askar_dotnet.Models;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Features.Handshakes.Common;
using Hyperledger.Aries.Features.IssueCredential;
using Hyperledger.Aries.Payments;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Utils;
using Hyperledger.Indy.DidApi;
using Hyperledger.Indy.PoolApi;
using Hyperledger.TestHarness;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Multiformats.Base;
using Polly;
using Xunit;
using IndyVdrMod = indy_vdr_dotnet.libindy_vdr.ModApi;
using AriesAskarKey = aries_askar_dotnet.AriesAskar.KeyApi;
using AriesAskarResult = aries_askar_dotnet.AriesAskar.ResultListApi;

namespace Hyperledger.Aries.Tests
{
    public abstract class RecordServiceTests
    {
        public IHost Host { get; set; }
        protected virtual string GetPoolName() => "TestPool";
        protected virtual string GetIssuerSeed() => null;

        protected IAgentContext Context { get; set; }
        protected IWalletRecordService RecordService;

        [Fact]
        public async Task CanStoreAndRetrieveRecordWithTags()
        {
            var record = new ConnectionRecord { Id = "123" };
            record.SetTag("tag1", "tagValue1");

            await RecordService.AddAsync(Context.AriesStorage, record);

            var retrieved = await RecordService.GetAsync<ConnectionRecord>(Context.AriesStorage, "123");

            Assert.NotNull(retrieved);
            Assert.Equal(retrieved.Id, record.Id);
            Assert.NotNull(retrieved.GetTag("tag1"));
            Assert.Equal("tagValue1", retrieved.GetTag("tag1"));
        }

        [Fact]
        public async Task CanStoreAndRetrieveRecordWithTagsUsingSearch()
        {
            var tagName = Guid.NewGuid().ToString();
            var tagValue = Guid.NewGuid().ToString();

            var record = new ConnectionRecord { Id = Guid.NewGuid().ToString() };
            record.SetTag(tagName, tagValue);

            await RecordService.AddAsync(Context.AriesStorage, record);

            var search =
                await RecordService.SearchAsync<ConnectionRecord>(Context.AriesStorage,
                    SearchQuery.Equal(tagName, tagValue), null, 100);

            var retrieved = search.Single();

            Assert.NotNull(retrieved);
            Assert.Equal(retrieved.Id, record.Id);
            Assert.NotNull(retrieved.GetTag(tagName));
            Assert.Equal(tagValue, retrieved.GetTag(tagName));
        }

        [Fact]
        public async Task CanUpdateRecordWithTags()
        {
            var tagName = Guid.NewGuid().ToString();
            var tagValue = Guid.NewGuid().ToString();

            var id = Guid.NewGuid().ToString();

            var record = new ConnectionRecord { Id = id };
            record.SetTag(tagName, tagValue);

            await RecordService.AddAsync(Context.AriesStorage, record);

            var retrieved = await RecordService.GetAsync<ConnectionRecord>(Context.AriesStorage, id);

            retrieved.MyDid = "123";
            retrieved.SetTag(tagName, "value");

            await RecordService.UpdateAsync(Context.AriesStorage, retrieved);

            var updated = await RecordService.GetAsync<ConnectionRecord>(Context.AriesStorage, id);

            Assert.NotNull(updated);
            Assert.Equal(updated.Id, record.Id);
            Assert.NotNull(updated.GetTag(tagName));
            Assert.Equal("value", updated.GetTag(tagName));
            Assert.Equal("123", updated.MyDid);
        }

        [Fact]
        public async Task ReturnsNullForNonExistentRecord()
        {
            var record = await RecordService.GetAsync<ConnectionRecord>(Context.AriesStorage, Guid.NewGuid().ToString());
            Assert.Null(record);
        }

        [Fact]
        public async Task ReturnsEmptyListForNonExistentRecord()
        {
            var record = await RecordService.SearchAsync<ConnectionRecord>(
                Context.AriesStorage,
                SearchQuery.Equal(Guid.NewGuid().ToString(), Guid.NewGuid().ToString()), null, 100);
            Assert.False(record.Any());
        }

        [Fact]
        public void InitialConnectionRecordIsInvitedAndHasTag()
        {
            var record = new ConnectionRecord();

            Assert.True(record.State == ConnectionState.Invited);
            Assert.True(record.GetTag(nameof(ConnectionRecord.State)) == ConnectionState.Invited.ToString("G"));
        }

        [Fact]
        public void InitialCredentialRecordIsOfferedAndHasTag()
        {
            var record = new CredentialRecord();

            Assert.True(record.State == CredentialState.Offered);
            Assert.True(record.GetTag(nameof(CredentialRecord.State)) == CredentialState.Offered.ToString("G"));
        }

        [Fact]
        public async Task CreatedAtPopulatedOnStoredRecord()
        {
            var record = new ConnectionRecord { Id = "123" };

            Assert.Null(record.CreatedAtUtc);

            await RecordService.AddAsync(Context.AriesStorage, record);

            var retrieved = await RecordService.GetAsync<ConnectionRecord>(Context.AriesStorage, "123");

            Assert.NotNull(retrieved);
            Assert.Equal(retrieved.Id, record.Id);
            Assert.NotNull(retrieved.CreatedAtUtc);
        }

        [Fact]
        public async Task UpdateAtPopulatedOnUpdatedRecord()
        {
            var record = new ConnectionRecord { Id = "123" };

            await RecordService.AddAsync(Context.AriesStorage, record);

            var retrieved = await RecordService.GetAsync<ConnectionRecord>(Context.AriesStorage, "123");

            Assert.NotNull(retrieved);
            Assert.Equal(retrieved.Id, record.Id);
            Assert.Null(retrieved.UpdatedAtUtc);

            await RecordService.UpdateAsync(Context.AriesStorage, retrieved);

            retrieved = await RecordService.GetAsync<ConnectionRecord>(Context.AriesStorage, "123");

            Assert.NotNull(retrieved);
            Assert.Equal(retrieved.Id, record.Id);
            Assert.NotNull(retrieved.UpdatedAtUtc);
        }

        [Fact]
        public async Task ReturnsRecordsFilteredByCreatedAt()
        {
            var record = new ConnectionRecord { Id = "123" };
            await RecordService.AddAsync(Context.AriesStorage, record);

            await Task.Delay(TimeSpan.FromSeconds(1));
            var now = DateTime.UtcNow;
            await Task.Delay(TimeSpan.FromSeconds(1));

            record = new ConnectionRecord { Id = "456" };
            await RecordService.AddAsync(Context.AriesStorage, record);

            var records = await RecordService.SearchAsync<ConnectionRecord>(
                Context.AriesStorage,
                SearchQuery.Greater(nameof(ConnectionRecord.CreatedAtUtc), now), null, 100);

            Assert.True(records.Count == 1);
            Assert.True(records[0].Id == "456");
        }
    }

    [Trait("Category", "DefaultV1")]
    public class RecordServiceTestsV1 : RecordServiceTests, IAsyncLifetime
    {
        public async Task DisposeAsync()
        {
            var walletOptions = Host.Services.GetService<IOptions<AgentOptions>>().Value;
            var walletService = Host.Services.GetService<IWalletService>();
            await Host.StopAsync();
            await walletService.DeleteWalletAsync(walletOptions.WalletConfiguration, walletOptions.WalletCredentials);
            Host.Dispose();
        }

        public async Task InitializeAsync()
        {
            Host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<ConsoleLifetimeOptions>(options =>
                    options.SuppressStatusMessages = true);
                services.AddAriesFramework(builder => builder
                    .RegisterAgent(options =>
                    {
                        options.WalletConfiguration = new WalletConfiguration { Id = Guid.NewGuid().ToString() };
                        options.WalletCredentials = new WalletCredentials { Key = "test" };
                        options.GenesisFilename = Path.GetFullPath("pool_genesis.txn");
                        options.PoolName = GetPoolName();
                        options.EndpointUri = "http://test";
                        options.IssuerKeySeed = GetIssuerSeed();
                    }));
            })
            .Build();

            await Host.StartAsync();
            await Pool.SetProtocolVersionAsync(2);

            RecordService = Host.Services.GetService<IWalletRecordService>();
            Context = await Host.Services.GetService<IAgentProvider>().GetContextAsync();
        }
    }

    [Trait("Category", "DefaultV2")]
    public class RecordServiceTestsV2 : RecordServiceTests, IAsyncLifetime
    {
        [Fact]
        public async Task CanStoreAndRetrieveKey()
        {
            //Arrange
            var keyHandle = await CryptoUtils.CreateKeyPair(KeyAlg.ED25519);
            var verKey = await AriesAskarKey.GetPublicBytesFromKeyAsync(keyHandle);
            string verKeyBase58 = Multibase.Base58.Encode(verKey);
            var secretKey = await AriesAskarKey.GetSecretBytesFromKeyAsync(keyHandle);
            string secretKeyBase58 = Multibase.Base58.Encode(secretKey);

            var initialKeyHandle = await RecordService.GetKeyAsync(Context.AriesStorage, verKeyBase58);
            Assert.Equal(default, initialKeyHandle);

            //Act
            await RecordService.AddKeyAsync(Context.AriesStorage, keyHandle, verKeyBase58);
            await RecordService.AddKeyAsync(Context.AriesStorage, keyHandle, verKeyBase58);
            var actualKeyHandle = await RecordService.GetKeyAsync(Context.AriesStorage, verKeyBase58);
            var actualVerkey = Multibase.Base58.Encode(await AriesAskarKey.GetPublicBytesFromKeyAsync(actualKeyHandle));
            var actualSecretKey = Multibase.Base58.Encode(await AriesAskarKey.GetSecretBytesFromKeyAsync(actualKeyHandle));

            //Assert
            Assert.NotEqual(default, actualKeyHandle);
            Assert.Equal(verKeyBase58, actualVerkey);
            Assert.Equal(secretKeyBase58, actualSecretKey);
        }

        public async Task DisposeAsync()
        {
            var walletOptions = Host.Services.GetService<IOptions<AgentOptions>>().Value;
            var walletService = Host.Services.GetService<IWalletService>();
            await Host.StopAsync();
            await walletService.DeleteWalletAsync(walletOptions.WalletConfiguration, walletOptions.WalletCredentials);
            Host.Dispose();
        }

        public async Task InitializeAsync()
        {
            Host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<ConsoleLifetimeOptions>(options =>
                    options.SuppressStatusMessages = true);
                services.AddAriesFrameworkV2(builder => builder
                    .RegisterAgentV2(options =>
                    {
                        options.WalletConfiguration = TestConstants.TestSingleWalletV2WalletConfig;
                        options.WalletCredentials = TestConstants.TestSingelWalletV2WalletCreds;
                        options.GenesisFilename = Path.GetFullPath("pool_genesis.txn");
                        options.PoolName = GetPoolName();
                        options.EndpointUri = "http://test";
                        options.IssuerKeySeed = GetIssuerSeed();
                    }));
            })
            .Build();

            await Host.StartAsync();
            await IndyVdrMod.SetProtocolVersionAsync(2);

            RecordService = Host.Services.GetService<IWalletRecordService>();
            Context = await Host.Services.GetService<IAgentProvider>().GetContextAsync();
        }
    }
}
