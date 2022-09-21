using System;
using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Ledger;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Aries.TestHarness;
using Hyperledger.Aries.Utils;
using Hyperledger.Indy.WalletApi;
using Hyperledger.TestHarness;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hyperledger.Aries.Tests
{
    public class DidUtilsTests : IClassFixture<DidUtilsTests.SingleTestWalletFixture>
    {
        private const string VALID_FULL_VERKEY = "MeHaPyPGsbBCgMKo13oWK7MeHaPyPGsbBCgMKo13oWK7";
        private const string ANOTHER_VALID_FULL_VERKEY = "XHhCzrFBTvrh2GsmHWRW4bpGYHdiPJbagSTFEMvFayc";
        private const string VALID_ABBREVIATED_VERKEY = "~MeHaPyPGsbBCgMKo13oWK7";
        private const string VALID_DID_KEY = "did:key:z6MkhaXgBZDvotDkL5257faiztiGiC2QtKLGpbnnEGta2doK";
        private const string ORIG_VERKEY = "8HH5gYEeNc3z7PYXmd54d4x6qAfCNrqQqEB3nS7Zfu7K";
        private const string DERIVED_DID_KEY = "did:key:z6MkmjY8GnV5i9YTDtPETC2uUAW6ejw3nk5mXF5yci5ab7th";
        private const string VALID_SECP256K1_0 = "did:key:zQ3shokFTS3brHcDQrn82RUDfCZESWL1ZdCEJwekUDPQiYBme";

        protected TestSingleWallet _fixture;
        private IAgentContext _agentContext;
        private readonly string _walletConfig = $"{{\"id\":\"{Guid.NewGuid()}\"}}";
        private const string Credentials = "{\"key\":\"test_wallet_key\"}";
        private static IWalletRecordService _recordService;
        private static ILedgerService _ledgerService;

        public class SingleTestWalletFixture : TestSingleWallet
        {
            protected override string GetIssuerSeed() => TestConstants.StewardSeed;
        }

        public DidUtilsTests(SingleTestWalletFixture fixture)
        {
            _fixture = fixture;
            _agentContext = _fixture.Host.Services.GetService<IAgentProvider>().GetContextAsync().GetAwaiter().GetResult();
            _recordService = _fixture.Host.Services.GetService<IWalletRecordService>();
            _ledgerService = _fixture.Host.Services.GetService<ILedgerService>();
        }

        [Fact]
        public void CanDetectFullVerkey()
        {
            Assert.True(DidUtils.IsFullVerkey(VALID_FULL_VERKEY)); //Valid full verkey
            Assert.True(DidUtils.IsFullVerkey(ANOTHER_VALID_FULL_VERKEY)); // Indy seems to generate verkeys with less than 256bit (only 43 chars)
            Assert.False(DidUtils.IsFullVerkey("")); 
            Assert.False(DidUtils.IsFullVerkey(VALID_ABBREVIATED_VERKEY)); //Valid abbreviated verkey
        }

        [Fact]
        public void CanDetectAbbreviatedVerkey()
        {
            Assert.True(DidUtils.IsAbbreviatedVerkey(VALID_ABBREVIATED_VERKEY));
            Assert.False(DidUtils.IsAbbreviatedVerkey(""));
            Assert.False(DidUtils.IsAbbreviatedVerkey(VALID_FULL_VERKEY));
        }

        [Fact]
        public void CanDetectVerkey()
        {
            Assert.True(DidUtils.IsVerkey(VALID_FULL_VERKEY));
            Assert.True(DidUtils.IsVerkey(VALID_ABBREVIATED_VERKEY));
            Assert.False(DidUtils.IsVerkey(""));
        }

        [Fact]
        public void CanDetectDidKey()
        {
            Assert.True(DidUtils.IsDidKey(VALID_DID_KEY));
            Assert.True(DidUtils.IsDidKey(VALID_SECP256K1_0));
            Assert.False(DidUtils.IsDidKey(VALID_FULL_VERKEY));
            Assert.False(DidUtils.IsDidKey(""));
        }
        
        [Fact]
        public void CanConvertDidKeyToVerkey()
        {
            var result = DidUtils.ConvertDidKeyToVerkey(DERIVED_DID_KEY);
            
            Assert.Equal(ORIG_VERKEY, result);
        }
        
        [Fact]
        public void CanConvertVerkeyToDidKey()
        {
            var result = DidUtils.ConvertVerkeyToDidKey(ORIG_VERKEY);
            
            Assert.Equal(DERIVED_DID_KEY, result);
        }

        [Fact]
        public void ConvertNonEd25519KeysToDidKeyWillThrowArgumentException()
        {
            Assert.Throws<ArgumentException>(() => DidUtils.ConvertDidKeyToVerkey(VALID_SECP256K1_0));
        }

        [Fact]
        public async Task KeyForDidAsyncGetsKey()
        {
            string tmpDid = "Th7MpTaRZVRYnPiabds81Y";
            string key = await DidUtils.KeyForDidAsync(_agentContext, _recordService, _ledgerService, tmpDid);

            Assert.True(DidUtils.IsVerkey(key));
        }
    }
}
