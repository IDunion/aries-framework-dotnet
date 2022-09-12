using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Features.IssueCredential;
using Hyperledger.Aries.Storage;
using Hyperledger.TestHarness;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Hyperledger.Aries.Tests
{
    public abstract class SchemaServiceTests //: TestSingleWallet
    {
        protected TestSingleWallet _fixture;
        protected IAgentContext Context { get; set; }
        protected ISchemaService SchemaService { get; set; }
        protected IProvisioningService ProvisioningService { get; set; }

        //TODO : ??? - Check if right services for V1 bzw. V2 are used. 
        // see: var schemaService = Host.Services.GetService<ISchemaService>();  _fixture is missing, solution
        // var schemaService = _fixture.Host.Services.GetService<ISchemaService>(); or add schemaService in TestSingleWallet as global like WalletRecordService and so on
        [Fact]
        public async Task CanCreateAndResolveSchema()
        {
            SchemaService = _fixture.Host.Services.GetService<ISchemaService>();
            ProvisioningService = _fixture.Host.Services.GetService<IProvisioningService>();
            Context = await _fixture.Host.Services.GetService<IAgentProvider>().GetContextAsync();

            var record = await ProvisioningService.GetProvisioningAsync(Context.AriesStorage);

            await _fixture.PromoteTrustAnchor(record.IssuerDid, record.IssuerVerkey);

            var schemaName = $"Test-Schema-{Guid.NewGuid().ToString("N")}";
            var schemaVersion = "1.0";
            var schemaAttrNames = new[] {"test_attr_1", "test_attr_2"};

            //Create a dummy schema
            var schemaId = await SchemaService.CreateSchemaAsync(Context, record.IssuerDid,
                schemaName, schemaVersion, schemaAttrNames);

            // Delay to allow ledger in container to catch up
            await Task.Delay(TimeSpan.FromSeconds(5));

            //Resolve it from the ledger with its identifier
            var resultSchema = await SchemaService.LookupSchemaAsync(Context, schemaId);

            var resultSchemaName = JObject.Parse(resultSchema)["name"].ToString();
            var resultSchemaVersion = JObject.Parse(resultSchema)["version"].ToString();
            var sequenceId = Convert.ToInt32(JObject.Parse(resultSchema)["seqNo"].ToString());

            Assert.Equal(schemaName, resultSchemaName);
            Assert.Equal(schemaVersion, resultSchemaVersion);

            //Resolve it from the ledger with its sequence Id
            var secondResultSchema = await SchemaService.LookupSchemaAsync(Context, sequenceId);

            var secondResultSchemaName = JObject.Parse(secondResultSchema)["name"].ToString();
            var secondResultSchemaVersion = JObject.Parse(secondResultSchema)["version"].ToString();

            Assert.Equal(schemaName, secondResultSchemaName);
            Assert.Equal(schemaVersion, secondResultSchemaVersion);
        }

        [Fact]
        public async Task CanCreateAndResolveCredentialDefinitionAndSchema()
        {
            SchemaService = _fixture.Host.Services.GetService<ISchemaService>();
            ProvisioningService = _fixture.Host.Services.GetService<IProvisioningService>();
            Context = await _fixture.Host.Services.GetService<IAgentProvider>().GetContextAsync();

            var record = await ProvisioningService.GetProvisioningAsync(Context.AriesStorage);

            await _fixture.PromoteTrustAnchor(record.IssuerDid, record.IssuerVerkey);

            var schemaName = $"Test-Schema-{Guid.NewGuid().ToString()}";
            var schemaVersion = "1.0";
            var schemaAttrNames = new[] { "test_attr_1", "test_attr_2" };

            //Create a dummy schema
            var schemaId = await SchemaService.CreateSchemaAsync(Context, record.IssuerDid,
                schemaName, schemaVersion, schemaAttrNames);

            // Ledger catch up
            await Task.Delay(TimeSpan.FromSeconds(2));

            var credId = await SchemaService.CreateCredentialDefinitionAsync(Context, schemaId,
                record.IssuerDid, "Tag", false, 100, new Uri("http://mock/tails"));

            // Ledger catch up
            await Task.Delay(TimeSpan.FromSeconds(2));

            var credDef =
                await SchemaService.LookupCredentialDefinitionAsync(Context, credId);

            var resultCredId = JObject.Parse(credDef)["id"].ToString();

            Assert.Equal(credId, resultCredId);

            var result = await SchemaService.LookupSchemaFromCredentialDefinitionAsync(Context, credId);

            var resultSchemaName = JObject.Parse(result)["name"].ToString();
            var resultSchemaVersion = JObject.Parse(result)["version"].ToString();

            Assert.Equal(schemaName, resultSchemaName);
            Assert.Equal(schemaVersion, resultSchemaVersion);

            var recordResult = await SchemaService.GetCredentialDefinitionAsync(Context.AriesStorage, credId);

            Assert.Equal(schemaId, recordResult.SchemaId);
        }

        [Trait("Category", "DefaultV1")]
        public class SchemaServiceTestsV1 : SchemaServiceTests, IClassFixture<SchemaServiceTestsV1.SingleTestWalletFixture>
        {
            public class SingleTestWalletFixture : TestSingleWallet
            {
            }
        
            public SchemaServiceTestsV1(SingleTestWalletFixture fixture)
            {
                _fixture = fixture;
            }
        }

        [Trait("Category", "DefaultV2")]
        public class SchemaServiceTestsV2 : SchemaServiceTests, IClassFixture<SchemaServiceTestsV2.SingleTestWalletV2Fixture>
        {
            public class SingleTestWalletV2Fixture : TestSingleWalletV2
            {
            }
        
            public SchemaServiceTestsV2(SingleTestWalletV2Fixture fixture)
            {
                _fixture = fixture;
            }
        }
    }
}
