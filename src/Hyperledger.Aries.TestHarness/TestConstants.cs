using Hyperledger.Aries.Storage;
using static Hyperledger.Aries.Storage.WalletConfiguration;
using System.IO;
using System;

namespace Hyperledger.TestHarness
{
    public static class TestConstants
    {
        public const string DefaultMockUri = "http://mock.com";

        public const string DefaultMasterSecret = "DefaultMasterSecret";

        public const string DefaultVerkey = "MeHaPyPGsbBCgMKo13oWK7MeHaPyPGsbBCgMKo13oWK7";

        public const string StewardSeed = "000000000000000000000000Steward1";

        public const string StewardDid = "Th7MpTaRZVRYnPiabds81Y";

        public static WalletConfiguration TestSingleWalletV2WalletConfig = new WalletConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            StorageType = "sqlite",
            StorageConfiguration = new WalletStorageConfiguration
            {
                Path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\test-db"))
            }
        };

        public static WalletCredentials TestSingelWalletV2WalletCreds = new WalletCredentials { Key = "key" };
}
}
