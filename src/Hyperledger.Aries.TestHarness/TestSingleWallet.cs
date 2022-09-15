using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Hyperledger.Aries;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Payments;
using Hyperledger.Aries.Storage;
using Hyperledger.Indy.DidApi;
using Hyperledger.Indy.LedgerApi;
using Hyperledger.Indy.PoolApi;
using Hyperledger.Indy.WalletApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;
using static Hyperledger.Aries.Storage.WalletConfiguration;
using IndyPayments = Hyperledger.Indy.PaymentsApi.Payments;
using AriesAskarStore = aries_askar_dotnet.AriesAskar.StoreApi;
using IndyVdrMod = indy_vdr_dotnet.libindy_vdr.ModApi;
using Hyperledger.Aries.Utils;
using Hyperledger.Aries.Features.Handshakes.DidExchange;
using System.Security.Cryptography.X509Certificates;

namespace Hyperledger.TestHarness
{
    public abstract class TestSingleWallet : IAsyncLifetime
    {
        protected IAgentContext Context { get; set; }
        public CreateAndStoreMyDidResult Trustee { get; protected set; }
        public CreateAndStoreMyDidResult Trustee2 { get; protected set; }
        public CreateAndStoreMyDidResult Trustee3 { get; protected set; }

        protected IProvisioningService provisioningService;
        protected IWalletRecordService recordService;
        protected IPaymentService paymentService;
        protected ILedgerService ledgerService;

        public IHost Host { get; set; }

        public virtual string GetPoolName() => "TestPool";
        protected virtual string GetIssuerSeed() => null;
        public virtual async Task DisposeAsync()
        {
            var walletOptions = Host.Services.GetService<IOptions<AgentOptions>>().Value;
            await Host.StopAsync();

            await Context.AriesStorage.Wallet.CloseAsync();
            await Wallet.DeleteWalletAsync(walletOptions.WalletConfiguration.ToJson(), walletOptions.WalletCredentials.ToJson());
            Host.Dispose();
        }

        /// <summary>
        /// Create a single wallet and enable payments
        /// </summary>
        /// <returns></returns>
        public virtual async Task InitializeAsync()
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

            provisioningService = Host.Services.GetService<IProvisioningService>();
            recordService = Host.Services.GetService<IWalletRecordService>();
            paymentService = Host.Services.GetService<IPaymentService>();
            ledgerService = Host.Services.GetService<ILedgerService>();
            
            Context = await Host.Services.GetService<IAgentProvider>().GetContextAsync();

            Trustee = await Did.CreateAndStoreMyDidAsync(Context.AriesStorage.Wallet,
                new { seed = "000000000000000000000000Trustee1" }.ToJson());
            Trustee2 = await PromoteTrustee("000000000000000000000000Trustee2");
            Trustee3 = await PromoteTrustee("000000000000000000000000Trustee3");
        }

        protected virtual async Task<CreateAndStoreMyDidResult> PromoteTrustee(string seed)
        {
            var trustee = await Did.CreateAndStoreMyDidAsync(Context.AriesStorage.Wallet, new { seed = seed }.ToJson());

            try
            {
                await ledgerService.RegisterNymAsync(Context, Trustee.Did, trustee.Did, trustee.VerKey, "TRUSTEE");
            }
            catch (Exception)
            {
                // Do nothing - this is expected if the trustee is already registered
            }

            return trustee;
        }

        public virtual async Task PromoteTrustAnchor(string did, string verkey)
        {
            try
            { 
                await ledgerService.RegisterNymAsync(Context, Trustee.Did, did, verkey, "ENDORSER");
            }
            catch (Exception)
            {
                // Do nothing - this is expected if the ENDORSER is already registered
            }
        }

        public virtual async Task PromoteTrustAnchor()
        {
            var record = await Host.Services.GetService<IProvisioningService>().GetProvisioningAsync(Context.AriesStorage);
            if (record.IssuerDid == null || record.IssuerVerkey == null)
                throw new AriesFrameworkException(ErrorCode.InvalidRecordData, "Agent not set up as issuer");

            await Ledger.SignAndSubmitRequestAsync((await Context.Pool).Pool, Context.AriesStorage.Wallet, Trustee.Did,
                await Ledger.BuildNymRequestAsync(Trustee.Did, record.IssuerDid, record.IssuerVerkey, null, "ENDORSER"));
        }

        protected async Task<string> TrusteeMultiSignAndSubmitRequestAsync(string request)
        {
            var singedRequest1 = await Ledger.MultiSignRequestAsync(Context.AriesStorage.Wallet, Trustee.Did, request);
            var singedRequest2 = await Ledger.MultiSignRequestAsync(Context.AriesStorage.Wallet, Trustee2.Did, singedRequest1);
            var singedRequest3 = await Ledger.MultiSignRequestAsync(Context.AriesStorage.Wallet, Trustee3.Did, singedRequest2);

            return await Ledger.SubmitRequestAsync((await Context.Pool).Pool, singedRequest3);
        }

        protected async Task FundDefaultAccountAsync(ulong amount)
        {
            var record = await provisioningService.GetProvisioningAsync(Context.AriesStorage);
            var addressRecord = await recordService.GetAsync<PaymentAddressRecord>(Context.AriesStorage, record.DefaultPaymentAddressId);

            // Mint tokens to the address to fund initially
            var request = await IndyPayments.BuildMintRequestAsync(Context.AriesStorage.Wallet, Trustee.Did,
                new[] { new { recipient = addressRecord.Address, amount = amount } }.ToJson(), null);
            await TrusteeMultiSignAndSubmitRequestAsync(request.Result);

            await paymentService.RefreshBalanceAsync(Context, addressRecord);
        }

        protected async Task FundAccountAsync(ulong amount, string address)
        {
            var request = await IndyPayments.BuildMintRequestAsync(Context.AriesStorage.Wallet, Trustee.Did,
                new[] { new { recipient = address, amount = amount } }.ToJson(), null);
            await TrusteeMultiSignAndSubmitRequestAsync(request.Result);
        }
    }

    public abstract class TestSingleWalletV2 : TestSingleWallet
    {
        public new DidRecord Trustee { get; protected set; }
        public new DidRecord Trustee2 { get; protected set; }
        public new DidRecord Trustee3 { get; protected set; }

        public override async Task DisposeAsync()
        {
            var walletOptions = Host.Services.GetService<IOptions<AgentOptions>>().Value;
            var walletService = Host.Services.GetService<IWalletService>();
            await Host.StopAsync();
            await walletService.DeleteWalletAsync(walletOptions.WalletConfiguration, walletOptions.WalletCredentials);
            Host.Dispose();
        }

        public override async Task InitializeAsync()
        {
            Host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.Configure<ConsoleLifetimeOptions>(options =>
                        options.SuppressStatusMessages = true);
                    services.AddAriesFrameworkV2(builder => builder
                        .RegisterAgent(options =>
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

            provisioningService = Host.Services.GetService<IProvisioningService>();
            recordService = Host.Services.GetService<IWalletRecordService>();
            paymentService = Host.Services.GetService<IPaymentService>();
            ledgerService = Host.Services.GetService<ILedgerService>();
            
            Context = await Host.Services.GetService<IAgentProvider>().GetContextAsync();

            (string did , string verkey) = await DidUtils.CreateAndStoreMyDidAsync(Context.AriesStorage, recordService, seed: "000000000000000000000000Trustee1");
            Trustee = new() { Did = did, Verkey = verkey };
            Trustee2 = await PromoteTrustee("000000000000000000000000Trustee2");
            Trustee3 = await PromoteTrustee("000000000000000000000000Trustee3");
        }

        public new async Task<DidRecord> PromoteTrustee(string seed)
        {
            (string trusteeDid, string trusteeVerkey) = await DidUtils.CreateAndStoreMyDidAsync(Context.AriesStorage, recordService, seed : seed);

            try
            {
                await ledgerService.RegisterNymAsync(Context, Trustee.Did, trusteeDid, trusteeVerkey, "TRUSTEE");
            }
            catch (Exception)
            {
                // Do nothing - this is expected if the trustee is already registered
            }

            return new DidRecord { Did = trusteeDid, Verkey = trusteeVerkey };
        }

        public override async Task PromoteTrustAnchor(string did, string verkey)
        {
            try
            {
                await ledgerService.RegisterNymAsync(Context, Trustee.Did, did, verkey, "ENDORSER");
            }
            catch (Exception)
            {
                // Do nothing - this is expected if the ENDORSER is already registered
            }
        }
    }
}
