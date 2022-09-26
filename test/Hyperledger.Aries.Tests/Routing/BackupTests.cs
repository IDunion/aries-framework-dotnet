using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Routing;
using Hyperledger.TestHarness.Mock;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Decorators.Attachments;
using Hyperledger.Aries.Storage;
using Hyperledger.Indy.DidApi;
using Microsoft.Extensions.Options;
using Xunit;
using Hyperledger.Aries.Utils;
using Hyperledger.Indy.PoolApi;
using Microsoft.Extensions.Hosting;

namespace Hyperledger.Aries.Tests.Routing
{
    public abstract class BackupTests
    {
        public InProcAgentV1.PairedAgents PairV1 { get; protected set; }
        public InProcAgentV2.PairedAgentsV2 PairV2 { get; protected set; }

        public IEdgeClientService EdgeClient { get; protected set; }
        public IAgentContext EdgeContext { get; protected set; }
        public AgentOptions AgentOptions { get; protected set; }
        public IAgentContext MediatorContext { get; protected set; }
        public IWalletService WalletService { get; protected set; }
        public IWalletRecordService RecordService { get; protected set; }



        [Fact(DisplayName = "Create backup with default seed")]
        public async Task CreateBackup()
        {
            var seed = "00000000000000000000000000000000";

            var path = SetupDirectoriesAndReturnPath(seed);
            
            await EdgeClient.CreateBackupAsync(EdgeContext, seed);
            var numDirsAfterBackup = Directory.GetDirectories(path).Length;
            var walletDir = Directory.GetDirectories(path).First();
            var backupDir = Directory.GetDirectories(walletDir).First();
            var backedUpWallet = Directory.GetFiles(backupDir).First();
            
            Assert.True(Directory.Exists(path));
            Assert.True(numDirsAfterBackup > 0);
            Assert.True(File.Exists(backedUpWallet));
        }
        
        [Fact(DisplayName = "Create backup with shorter seed throws ArgumentException")]
        public async Task CreateBackupWithShortSeed()
        {
            var seed = "11112222";
            SetupDirectoriesAndReturnPath(seed);
            
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => EdgeClient.CreateBackupAsync(EdgeContext, seed));
            Assert.Equal(ex.Message, $"{nameof(seed)} should be 32 characters");
        }

        [Fact(DisplayName = "Get a list of available backups")]
        public async Task ListBackups()
        {
            // change backupId to be retrieved from provisioning service
            // Wait one second
            await Task.Delay(TimeSpan.FromSeconds(1));
            
            var seed = "00000000000000000000000000000000";
            SetupDirectoriesAndReturnPath(seed);

            await EdgeClient.CreateBackupAsync(EdgeContext, seed);
            var result = await EdgeClient.ListBackupsAsync(EdgeContext);

            Assert.NotEmpty(result);
        }

        [Fact(DisplayName = "Retrieve latest backup")]
        public async Task RetrieveLatestBackup()
        {
            var seed = "00000000000000000000000000000000";

            SetupDirectoriesAndReturnPath(seed);
            await EdgeClient.CreateBackupAsync(EdgeContext, seed);
            
            var result = await EdgeClient.RetrieveBackupAsync(EdgeContext, seed);
            
            Assert.NotEmpty(result);
            Assert.IsType<Attachment>(result.First());
        }

        [Fact(DisplayName = "Restore edge agent from backup")]
        public async Task RestoreAgentFromBackup()
        {
            var seed = "00000000000000000000000000000000";
            var path = SetupDirectoriesAndReturnPath(seed);
            var (myDid, myVerkey) = await DidUtils.CreateAndStoreMyDidAsync(EdgeContext.AriesStorage, RecordService);
            await EdgeClient.CreateBackupAsync(EdgeContext, seed);
            // Create a DID that we will retrieve and compare from imported wallet
            
            var attachments = await EdgeClient.RetrieveBackupAsync(EdgeContext, seed);
            await EdgeClient.RestoreFromBackupAsync(EdgeContext, seed, attachments);

            var newWallet = await WalletService.GetWalletAsync(AgentOptions.WalletConfiguration, AgentOptions.WalletCredentials);
            
            var myKey = await DidUtils.KeyForLocalDidAsync(newWallet, RecordService, myDid);
            Assert.Equal(myKey, myVerkey);
        }

        private string SetupDirectoriesAndReturnPath(string seed)
        {
            var edgeWallet = Path.Combine(Path.GetTempPath(), seed);

            if (File.Exists(edgeWallet))
            {
                File.Delete(edgeWallet);
            }
            
            var path = Path.Combine(Path.GetTempPath(), "AriesBackups");

            var walletDirExists = Directory.Exists(path);

            if (walletDirExists)
            {
                Directory.Delete(path, true);
            }

            return path;
        }
    }

    [Trait("Category", "DefaultV1")]
    public class BackupTestsV1 : BackupTests, IAsyncLifetime
    {
        public async Task DisposeAsync()
        {
            await PairV1.Agent1.DisposeAsync();
            await PairV1.Agent2.DisposeAsync();
        }

        public async Task InitializeAsync()
        {
            // Agent1 - Mediator
            // Agent2 - Edge
            PairV1 = await InProcAgentV1.CreatePairedWithRoutingAsync();

            // WalletService = Pair.Agent2.Host.Services.GetRequiredService<IWalletService>();
            EdgeClient = PairV1.Agent2.Host.Services.GetRequiredService<IEdgeClientService>();
            WalletService = PairV1.Agent2.Host.Services.GetRequiredService<IWalletService>();
            AgentOptions = PairV1.Agent2.Host.Services.GetRequiredService<IOptions<AgentOptions>>().Value;
            RecordService = PairV1.Agent2.Host.Services.GetRequiredService<IWalletRecordService>();
            EdgeContext = PairV1.Agent2.Context;
            MediatorContext = PairV1.Agent1.Context;
        }
    }

    [Trait("Category", "DefaultV2")]
    public class BackupTestsV2 : BackupTests, IAsyncLifetime
    {
        public async Task DisposeAsync()
        {
            await PairV2.Agent1.DisposeAsync();
            await PairV2.Agent2.DisposeAsync();
        }

        public async Task InitializeAsync()
        {
            // Agent1 - Mediator
            // Agent2 - Edge
            PairV2 = await InProcAgentV2.CreatePairedWithRoutingAsync();

            // WalletService = Pair.Agent2.Host.Services.GetRequiredService<IWalletService>();
            EdgeClient = PairV2.Agent2.Host.Services.GetRequiredService<IEdgeClientService>();
            WalletService = PairV2.Agent2.Host.Services.GetRequiredService<IWalletService>();
            AgentOptions = PairV2.Agent2.Host.Services.GetRequiredService<IOptions<AgentOptions>>().Value;
            RecordService = PairV2.Agent2.Host.Services.GetRequiredService<IWalletRecordService>();
            EdgeContext = PairV2.Agent2.Context;
            MediatorContext = PairV2.Agent1.Context;
        }
    }
}
