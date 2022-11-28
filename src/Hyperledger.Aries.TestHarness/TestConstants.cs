using Hyperledger.Aries.Storage;
using static Hyperledger.Aries.Storage.WalletConfiguration;
using System.IO;
using System;
using aries_askar_dotnet.AriesAskar;

namespace Hyperledger.TestHarness
{
    public static class TestConstants
    {
        public const string DefaultMockUri = "http://mock.com";

        public const string DefaultMasterSecret = "DefaultMasterSecret";

        public const string DefaultVerkey = "MeHaPyPGsbBCgMKo13oWK7MeHaPyPGsbBCgMKo13oWK7";

        public const string StewardSeed = "000000000000000000000000Steward1";

        public const string StewardDid = "Th7MpTaRZVRYnPiabds81Y";

        public const string WalletSeed = "000000000000000000000000Wallet11";

        public const string NewWalletSeed = "101010302050307003000NewWallet99";

        public const string RecipientSeed = "00000000000000000000000Recipient"; 

        public const string SenderSeed = "00000000000000000000000000Sender";

        public static WalletConfiguration TestSingleWalletV2WalletConfig = new WalletConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            StorageType = "sqlite",
            StorageConfiguration = new WalletStorageConfiguration
            {
                Path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\test-db"))
            }
        };

        public static WalletConfiguration TestSingleWalletV2HolderConfig = new WalletConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            StorageType = "sqlite",
            StorageConfiguration = new WalletStorageConfiguration
            {
                Path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\test-holder-db"))
            }
        };

        public static WalletConfiguration TestSingleWalletV2IssuerConfig = new WalletConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            StorageType = "sqlite",
            StorageConfiguration = new WalletStorageConfiguration
            {
                Path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\test-issuer-db"))
            }
        };

        public static WalletConfiguration TestWalletV2EdgeConfig = new WalletConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            StorageType = "sqlite",
            StorageConfiguration = new WalletStorageConfiguration
            {
                Path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\test-edge-db"))
            }
        };

        public static WalletConfiguration TestWalletV2MediatorConfig = new WalletConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            StorageType = "sqlite",
            StorageConfiguration = new WalletStorageConfiguration
            {
                Path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\test-mediator-db"))
            }
        };

        public static WalletCredentials TestSingelWalletV2WalletCreds = new WalletCredentials { Key = "key" };
        public static WalletCredentials TestSingelWalletV2HolderCreds = new WalletCredentials { Key = "holder-key" };
        public static WalletCredentials TestSingelWalletV2IssuerCreds = new WalletCredentials { Key = "issuer-key" };
        public static WalletCredentials TestWalletV2EdgeCreds = new WalletCredentials { Key = "edge-key" };
        public static WalletCredentials TestWalletV2MediatorCreds = new WalletCredentials { Key = "mediator-key" };
        public static WalletCredentials TestSingelWalletCredsRawEncoding = new WalletCredentials { 
            Key = StoreApi.GenerateRawKeyAsync(WalletSeed).GetAwaiter().GetResult(), 
            KeyDerivationMethod = "raw" };
    }
}
