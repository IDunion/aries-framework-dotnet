using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.IssueCredential;
using Hyperledger.Aries.Features.PresentProof;
using Hyperledger.Aries.Models.Events;
using Hyperledger.Aries.Models.Records;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.TestHarness;
using Hyperledger.Indy.AnonCredsApi;
using Hyperledger.TestHarness;
using Hyperledger.TestHarness.Mock;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Hyperledger.TestHarness.Mock.InProcAgentV1;
using SharedRsPresReq = indy_shared_rs_dotnet.IndyCredx.PresentationRequestApi;

namespace Hyperledger.Aries.Tests.Protocols
{
    public class RevocationTestsFixtureV1 : TestSingleWallet
    {
        public InProcAgentV1.PairedAgents PairedAgents;

        public IAgentContext IssuerAgentContext;
        public IAgentContext HolderAgentContext;

        public ICredentialService IssuerCredentialService;
        public ICredentialService HolderCredentialService;

        public IEventAggregator EventAggregator;

        public IProofService IssuerProofService;
        public IProofService HolderProofService;

        public IMessageService IssuerMessageService;
        public IMessageService HolderMessageService;

        public ProvisioningRecord IssuerConfiguration;

        public string RevocableCredentialDefinitionId;
        public string NonRevocableCredentialDefinitionId;

        private string _credentialSchemaId;

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            PairedAgents = await InProcAgentV1.CreatePairedAsync(true);

            IssuerAgentContext = PairedAgents.Agent1.Context;
            HolderAgentContext = PairedAgents.Agent2.Context;

            EventAggregator = PairedAgents.Agent2.Provider.GetService<IEventAggregator>();

            IssuerCredentialService = PairedAgents.Agent1.Provider.GetService<ICredentialService>();
            HolderCredentialService = PairedAgents.Agent2.Provider.GetService<ICredentialService>();

            IssuerProofService = PairedAgents.Agent1.Provider.GetService<IProofService>();
            HolderProofService = PairedAgents.Agent2.Provider.GetService<IProofService>();

            IssuerMessageService = PairedAgents.Agent1.Provider.GetRequiredService<IMessageService>();
            HolderMessageService = PairedAgents.Agent2.Provider.GetService<IMessageService>();

            IssuerConfiguration = await PairedAgents.Agent1.Provider.GetRequiredService<IProvisioningService>()
                .GetProvisioningAsync(IssuerAgentContext.AriesStorage);
            await PromoteTrustAnchor(IssuerConfiguration.IssuerDid, IssuerConfiguration.IssuerVerkey);

            _credentialSchemaId = await PairedAgents.Agent1.Provider.GetRequiredService<ISchemaService>()
                .CreateSchemaAsync(
                    context: IssuerAgentContext,
                    issuerDid: IssuerConfiguration.IssuerDid,
                    name: $"test-schema-{Guid.NewGuid()}",
                    version: "1.0",
                    attributeNames: new[] { "name", "age" });

            RevocableCredentialDefinitionId = await PairedAgents.Agent1.Provider.GetRequiredService<ISchemaService>()
                .CreateCredentialDefinitionAsync(
                    context: IssuerAgentContext,
                    new CredentialDefinitionConfiguration
                    {
                        SchemaId = _credentialSchemaId,
                        EnableRevocation = true,
                        RevocationRegistryBaseUri = "http://localhost",
                        Tag = "revoc"
                    });

            NonRevocableCredentialDefinitionId = await PairedAgents.Agent1.Provider.GetRequiredService<ISchemaService>()
                .CreateCredentialDefinitionAsync(
                    context: IssuerAgentContext,
                    new CredentialDefinitionConfiguration
                    {
                        SchemaId = _credentialSchemaId,
                        EnableRevocation = false,
                        RevocationRegistryBaseUri = "http://localhost",
                        Tag = "norevoc"
                    });
        }

        public override async Task DisposeAsync()
        {
            await base.DisposeAsync();
            await PairedAgents.Agent1.DisposeAsync();
            await PairedAgents.Agent2.DisposeAsync();
        }
    }

    public class RevocationTestsV1 : IClassFixture<RevocationTestsFixtureV1>, IAsyncLifetime
    {
        private readonly RevocationTestsFixtureV1 _fixture;
        private readonly uint _now = (uint)DateTimeOffset.Now.ToUnixTimeSeconds();

        public RevocationTestsV1(RevocationTestsFixtureV1 data)
        {
            _fixture = data;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            foreach (var credentialRecord in await _fixture.HolderCredentialService.ListAsync(_fixture.HolderAgentContext))
                await _fixture.HolderCredentialService.DeleteCredentialAsync(
                    _fixture.HolderAgentContext, credentialRecord.Id);

            foreach (var credentialRecord in await _fixture.IssuerCredentialService.ListAsync(_fixture.IssuerAgentContext))
                await _fixture.IssuerCredentialService.DeleteCredentialAsync(
                    _fixture.IssuerAgentContext, credentialRecord.Id);
        }

        [Fact(DisplayName = "Test credential revocation")]
        public async Task CanRevokeCredential()
        {
            var receivedRevocationNotificationMessage = false;
            var receivedRevocationNotificationAckMessage = false;

            _fixture.EventAggregator.GetEventByType<ServiceMessageProcessingEvent>()
                .Where(x => x.MessageType == MessageTypesHttps.RevocationNotification)
                .Subscribe(_ => receivedRevocationNotificationMessage = true);

            _fixture.EventAggregator.GetEventByType<ServiceMessageProcessingEvent>()
                .Where(x => x.MessageType == MessageTypesHttps.RevocationNotification)
                .Subscribe(_ => receivedRevocationNotificationAckMessage = true);


            var (offer, record) = await _fixture.IssuerCredentialService
                .CreateOfferAsync(_fixture.IssuerAgentContext, new OfferConfiguration
                {
                    CredentialDefinitionId = _fixture.RevocableCredentialDefinitionId,
                    IssuerDid = _fixture.IssuerConfiguration.IssuerDid,
                    CredentialAttributeValues = new[]
                    {
                        new CredentialPreviewAttribute("name", "random"),
                        new CredentialPreviewAttribute("age", "22")
                    }
                });
            await _fixture.IssuerMessageService.SendAsync(_fixture.IssuerAgentContext, offer, _fixture.PairedAgents.Connection1);

            var credentialRecordOnHolderSide = (await _fixture.HolderCredentialService.ListAsync(_fixture.HolderAgentContext))
                .First(credentialRecord => credentialRecord.State == CredentialState.Offered);
            var (request, _) = await _fixture.HolderCredentialService.CreateRequestAsync(_fixture.HolderAgentContext, credentialRecordOnHolderSide.Id);
            await _fixture.HolderMessageService.SendAsync(_fixture.HolderAgentContext, request, _fixture.PairedAgents.Connection2);

            var credentialRecordOnIssuerSide = (await _fixture.IssuerCredentialService.ListRequestsAsync(
                _fixture.IssuerAgentContext)).First();
            var (issue, _) = await _fixture.IssuerCredentialService.CreateCredentialAsync(_fixture.IssuerAgentContext, credentialRecordOnIssuerSide.Id);
            await _fixture.IssuerMessageService.SendAsync(_fixture.IssuerAgentContext, issue, _fixture.PairedAgents.Connection1);

            credentialRecordOnHolderSide =
                await _fixture.HolderCredentialService.GetAsync(_fixture.HolderAgentContext,
                    credentialRecordOnHolderSide.Id);
            credentialRecordOnIssuerSide =
                await _fixture.IssuerCredentialService.GetAsync(_fixture.IssuerAgentContext,
                    credentialRecordOnIssuerSide.Id);

            Assert.Equal(CredentialState.Issued, credentialRecordOnHolderSide.State);
            Assert.Equal(CredentialState.Issued, credentialRecordOnIssuerSide.State);

            await _fixture.IssuerCredentialService.RevokeCredentialAsync(
                _fixture.IssuerAgentContext, credentialRecordOnIssuerSide.Id, true);

            Assert.True(
                await _fixture.HolderProofService.IsRevokedAsync(
                    _fixture.HolderAgentContext,
                    credentialRecordOnHolderSide.Id));
            Assert.True(
                await _fixture.IssuerProofService.IsRevokedAsync(
                    _fixture.IssuerAgentContext,
                    credentialRecordOnIssuerSide.Id));

            Assert.True(receivedRevocationNotificationMessage);
            Assert.True(receivedRevocationNotificationAckMessage);
        }

        [Fact(DisplayName = "Test verification without revocation")]
        public async Task CanVerifyWithoutRevocation()
        {
            var (offer, record) = await _fixture.IssuerCredentialService
                .CreateOfferAsync(_fixture.IssuerAgentContext, new OfferConfiguration
                {
                    CredentialDefinitionId = _fixture.NonRevocableCredentialDefinitionId,
                    IssuerDid = _fixture.IssuerConfiguration.IssuerDid,
                    CredentialAttributeValues = new[]
                    {
                        new CredentialPreviewAttribute("name", "random"),
                        new CredentialPreviewAttribute("age", "22")
                    }
                });
            await _fixture.IssuerMessageService
                .SendAsync(_fixture.IssuerAgentContext, offer, _fixture.PairedAgents.Connection1);

            var credentialRecordOnHolderSide = (await _fixture.HolderCredentialService.ListAsync(_fixture.HolderAgentContext))
                .First(credentialRecord => credentialRecord.State == CredentialState.Offered);
            var (request, _) = await _fixture.HolderCredentialService.CreateRequestAsync(_fixture.HolderAgentContext, credentialRecordOnHolderSide.Id);
            await _fixture.HolderMessageService.SendAsync(_fixture.HolderAgentContext, request, _fixture.PairedAgents.Connection2);

            var credentialRecordOnIssuerSide = (await _fixture.IssuerCredentialService.ListRequestsAsync(
                _fixture.IssuerAgentContext)).First();
            var (issue, _) = await _fixture.IssuerCredentialService.CreateCredentialAsync(_fixture.IssuerAgentContext, credentialRecordOnIssuerSide.Id);
            await _fixture.IssuerMessageService.SendAsync(_fixture.IssuerAgentContext, issue, _fixture.PairedAgents.Connection1);

            credentialRecordOnHolderSide =
                await _fixture.HolderCredentialService.GetAsync(_fixture.HolderAgentContext,
                    credentialRecordOnHolderSide.Id);
            credentialRecordOnIssuerSide =
                await _fixture.IssuerCredentialService.GetAsync(_fixture.IssuerAgentContext,
                    credentialRecordOnIssuerSide.Id);

            Assert.Equal(CredentialState.Issued, credentialRecordOnHolderSide.State);
            Assert.Equal(CredentialState.Issued, credentialRecordOnIssuerSide.State);

            var (requestPresentationMessage, proofRecordIssuer) = await _fixture.IssuerProofService
                .CreateRequestAsync(_fixture.IssuerAgentContext, new ProofRequest
                {
                    Name = "Test Verification",
                    Version = "1.0",
                    Nonce = await AnonCreds.GenerateNonceAsync(),
                    RequestedAttributes = new Dictionary<string, ProofAttributeInfo>
                    {
                        { "id-verification", new ProofAttributeInfo { Names = new [] { "name", "age" } } }
                    }
                });

            var proofRecordHolder = await _fixture.HolderProofService.ProcessRequestAsync(_fixture.HolderAgentContext, requestPresentationMessage, _fixture.PairedAgents.Connection2);
            var availableCredentials = await _fixture.HolderProofService.ListCredentialsForProofRequestAsync(_fixture.HolderAgentContext, proofRecordHolder.RequestJson.ToObject<ProofRequest>(), "id-verification");

            var (presentationMessage, _) = await _fixture.HolderProofService.CreatePresentationAsync(
                _fixture.HolderAgentContext, proofRecordHolder.Id, new RequestedCredentials
                {
                    RequestedAttributes = new Dictionary<string, RequestedAttribute>
                    {
                        { "id-verification", new RequestedAttribute
                            {
                                CredentialId = availableCredentials.First().CredentialInfo.Referent,
                                Revealed = true
                            }
                        }
                    }
                });

            proofRecordIssuer = await _fixture.IssuerProofService.ProcessPresentationAsync(_fixture.IssuerAgentContext, presentationMessage);
            var valid = await _fixture.IssuerProofService.VerifyProofAsync(_fixture.IssuerAgentContext, proofRecordIssuer.Id);

            Assert.True(valid);
            Assert.False(await _fixture.HolderProofService.IsRevokedAsync(_fixture.HolderAgentContext, availableCredentials.First().CredentialInfo.Referent));
        }

        [Fact(DisplayName = "Test verification with NonRevoked set on proof request level")]
        public async Task CanVerifyWithNonRevokedSetOnProofRequestLevel()
        {
            var (offer, record) = await _fixture.IssuerCredentialService
                .CreateOfferAsync(_fixture.IssuerAgentContext, new OfferConfiguration
                {
                    CredentialDefinitionId = _fixture.RevocableCredentialDefinitionId,
                    IssuerDid = _fixture.IssuerConfiguration.IssuerDid,
                    CredentialAttributeValues = new[]
                    {
                        new CredentialPreviewAttribute("name", "random"),
                        new CredentialPreviewAttribute("age", "22")
                    }
                });
            await _fixture.IssuerMessageService.SendAsync(_fixture.IssuerAgentContext, offer, _fixture.PairedAgents.Connection1);

            var credentialRecordOnHolderSide = (await _fixture.HolderCredentialService.ListAsync(_fixture.HolderAgentContext))
                .First(credentialRecord => credentialRecord.State == CredentialState.Offered);
            var (request, _) = await _fixture.HolderCredentialService.CreateRequestAsync(_fixture.HolderAgentContext, credentialRecordOnHolderSide.Id);
            await _fixture.HolderMessageService.SendAsync(_fixture.HolderAgentContext, request, _fixture.PairedAgents.Connection2);

            var credentialRecordOnIssuerSide = (await _fixture.IssuerCredentialService.ListRequestsAsync(
                _fixture.IssuerAgentContext)).First();
            var (issuance, _) = await _fixture.IssuerCredentialService.CreateCredentialAsync(_fixture.IssuerAgentContext, credentialRecordOnIssuerSide.Id);
            await _fixture.IssuerMessageService.SendAsync(_fixture.IssuerAgentContext, issuance, _fixture.PairedAgents.Connection1);

            credentialRecordOnHolderSide =
                await _fixture.HolderCredentialService.GetAsync(_fixture.HolderAgentContext,
                    credentialRecordOnHolderSide.Id);
            credentialRecordOnIssuerSide =
                await _fixture.IssuerCredentialService.GetAsync(_fixture.IssuerAgentContext,
                    credentialRecordOnIssuerSide.Id);

            Assert.Equal(CredentialState.Issued, credentialRecordOnHolderSide.State);
            Assert.Equal(CredentialState.Issued, credentialRecordOnIssuerSide.State);

            var (requestPresentationMessage, proofRecordIssuer) = await _fixture.IssuerProofService
                .CreateRequestAsync(_fixture.IssuerAgentContext, new ProofRequest
                {
                    Name = "Test Verification",
                    Version = "1.0",
                    Nonce = await AnonCreds.GenerateNonceAsync(),
                    RequestedAttributes = new Dictionary<string, ProofAttributeInfo>
                    {
                        { "id-verification", new ProofAttributeInfo { Names = new [] { "name", "age" } } }
                    },
                    NonRevoked = new RevocationInterval
                    {
                        From = 0,
                        To = _now
                    }
                });

            var proofRecordHolder = await _fixture.HolderProofService.ProcessRequestAsync(_fixture.HolderAgentContext, requestPresentationMessage, _fixture.PairedAgents.Connection2);
            var availableCredentials = await _fixture.HolderProofService
                .ListCredentialsForProofRequestAsync(_fixture.HolderAgentContext, proofRecordHolder.RequestJson.ToObject<ProofRequest>(), "id-verification");

            var (presentationMessage, _) = await _fixture.HolderProofService.CreatePresentationAsync(
                _fixture.HolderAgentContext, proofRecordHolder.Id, new RequestedCredentials
                {
                    RequestedAttributes = new Dictionary<string, RequestedAttribute>
                    {
                        { "id-verification", new RequestedAttribute
                            {
                                CredentialId = availableCredentials.First().CredentialInfo.Referent,
                                Revealed = true
                            }
                        }
                    }
                });
            proofRecordIssuer = await _fixture.IssuerProofService.ProcessPresentationAsync(_fixture.IssuerAgentContext, presentationMessage);

            var valid = await _fixture.IssuerProofService.VerifyProofAsync(_fixture.IssuerAgentContext, proofRecordIssuer.Id);
            Assert.True(valid);
        }

        [Fact(DisplayName = "Test verification with NonRevoked set on attribute level")]
        public async Task CanVerifyWithNonRevokedSetOnAttributeLevel()
        {
            var (offer, record) = await _fixture.IssuerCredentialService
                .CreateOfferAsync(_fixture.IssuerAgentContext, new OfferConfiguration
                {
                    CredentialDefinitionId = _fixture.RevocableCredentialDefinitionId,
                    IssuerDid = _fixture.IssuerConfiguration.IssuerDid,
                    CredentialAttributeValues = new[]
                    {
                        new CredentialPreviewAttribute("name", "random"),
                        new CredentialPreviewAttribute("age", "22")
                    }
                });
            await _fixture.IssuerMessageService.SendAsync(_fixture.IssuerAgentContext, offer, _fixture.PairedAgents.Connection1);

            var credentialRecordOnHolderSide = (await _fixture.HolderCredentialService.ListAsync(_fixture.HolderAgentContext))
                .First(credentialRecord => credentialRecord.State == CredentialState.Offered);
            var (request, _) = await _fixture.HolderCredentialService.CreateRequestAsync(_fixture.HolderAgentContext, credentialRecordOnHolderSide.Id);
            await _fixture.HolderMessageService.SendAsync(_fixture.HolderAgentContext, request, _fixture.PairedAgents.Connection2);

            var credentialRecordOnIssuerSide = (await _fixture.IssuerCredentialService.ListRequestsAsync(
                _fixture.IssuerAgentContext)).First();
            var (issuance, _) = await _fixture.IssuerCredentialService.CreateCredentialAsync(_fixture.IssuerAgentContext, credentialRecordOnIssuerSide.Id);
            await _fixture.IssuerMessageService.SendAsync(_fixture.IssuerAgentContext, issuance, _fixture.PairedAgents.Connection1);

            credentialRecordOnHolderSide =
                await _fixture.HolderCredentialService.GetAsync(_fixture.HolderAgentContext,
                    credentialRecordOnHolderSide.Id);
            credentialRecordOnIssuerSide =
                await _fixture.IssuerCredentialService.GetAsync(_fixture.IssuerAgentContext,
                    credentialRecordOnIssuerSide.Id);

            Assert.Equal(CredentialState.Issued, credentialRecordOnHolderSide.State);
            Assert.Equal(CredentialState.Issued, credentialRecordOnIssuerSide.State);

            var (requestPresentationMessage, proofRecordIssuer) = await _fixture.IssuerProofService
                .CreateRequestAsync(_fixture.IssuerAgentContext, new ProofRequest
                {
                    Name = "Test Verification",
                    Version = "1.0",
                    Nonce = await AnonCreds.GenerateNonceAsync(),
                    RequestedAttributes = new Dictionary<string, ProofAttributeInfo>
                    {
                        { "id-verification", new ProofAttributeInfo
                            {
                                Names = new [] { "name", "age" },
                                NonRevoked = new RevocationInterval
                                {
                                    From = 0,
                                    To = _now
                                }
                            }
                        }
                    }
                });

            var proofRecordHolder = await _fixture.HolderProofService.ProcessRequestAsync(_fixture.HolderAgentContext, requestPresentationMessage, _fixture.PairedAgents.Connection2);
            var temp =
                await _fixture.HolderCredentialService.GetAsync(_fixture.HolderAgentContext,
                    credentialRecordOnHolderSide.Id);
            var availableCredentials = await _fixture.HolderProofService
                .ListCredentialsForProofRequestAsync(_fixture.HolderAgentContext, proofRecordHolder.RequestJson.ToObject<ProofRequest>(), "id-verification");

            var (presentationMessage, _) = await _fixture.HolderProofService.CreatePresentationAsync(
                _fixture.HolderAgentContext, proofRecordHolder.Id, new RequestedCredentials
                {
                    RequestedAttributes = new Dictionary<string, RequestedAttribute>
                    {
                        { "id-verification", new RequestedAttribute
                            {
                                CredentialId = availableCredentials.First().CredentialInfo.Referent,
                                Revealed = true
                            }
                        }
                    }
                });

            proofRecordIssuer = await _fixture.IssuerProofService.ProcessPresentationAsync(_fixture.IssuerAgentContext, presentationMessage);

            var valid = await _fixture.IssuerProofService.VerifyProofAsync(_fixture.IssuerAgentContext, proofRecordIssuer.Id);
            Assert.True(valid);
        }
    }

    public class RevocationTestsV2 : TestSingleWalletV2, IAsyncLifetime
    {
        private readonly uint _now = (uint)DateTimeOffset.Now.ToUnixTimeSeconds();

        public InProcAgentV2.PairedAgentsV2 PairedAgents;

        public IAgentContext IssuerAgentContext;
        public IAgentContext HolderAgentContext;

        public ICredentialService IssuerCredentialService;
        public ICredentialService HolderCredentialService;

        public IEventAggregator EventAggregator;

        public IProofService IssuerProofService;
        public IProofService HolderProofService;

        public IMessageService IssuerMessageService;
        public IMessageService HolderMessageService;

        public IWalletRecordService RecordService;

        public ProvisioningRecord IssuerConfiguration;

        public string RevocableCredentialDefinitionId;
        public string NonRevocableCredentialDefinitionId;

        private string _credentialSchemaId;

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            PairedAgents = await InProcAgentV2.CreatePairedAsync(true);

            IssuerAgentContext = PairedAgents.Agent1.Context;
            HolderAgentContext = PairedAgents.Agent2.Context;

            EventAggregator = PairedAgents.Agent2.Provider.GetService<IEventAggregator>();

            IssuerCredentialService = PairedAgents.Agent1.Provider.GetService<ICredentialService>();
            HolderCredentialService = PairedAgents.Agent2.Provider.GetService<ICredentialService>();

            IssuerProofService = PairedAgents.Agent1.Provider.GetService<IProofService>();
            HolderProofService = PairedAgents.Agent2.Provider.GetService<IProofService>();

            IssuerMessageService = PairedAgents.Agent1.Provider.GetRequiredService<IMessageService>();
            HolderMessageService = PairedAgents.Agent2.Provider.GetService<IMessageService>();

            RecordService = PairedAgents.Agent1.Provider.GetService<IWalletRecordService>();

            IssuerConfiguration = await PairedAgents.Agent1.Provider.GetRequiredService<IProvisioningService>()
                .GetProvisioningAsync(IssuerAgentContext.AriesStorage);
            await PromoteTrustAnchor(IssuerConfiguration.IssuerDid, IssuerConfiguration.IssuerVerkey);

            _credentialSchemaId = await PairedAgents.Agent1.Provider.GetRequiredService<ISchemaService>()
                .CreateSchemaAsync(
                    context: IssuerAgentContext,
                    issuerDid: IssuerConfiguration.IssuerDid,
                    name: $"test-schema-{Guid.NewGuid()}",
                    version: "1.0",
                    attributeNames: new[] { "name", "age" });

            RevocableCredentialDefinitionId = await PairedAgents.Agent1.Provider.GetRequiredService<ISchemaService>()
                .CreateCredentialDefinitionAsync(
                    context: IssuerAgentContext,
                    new CredentialDefinitionConfiguration
                    {
                        SchemaId = _credentialSchemaId,
                        EnableRevocation = true,
                        RevocationRegistryBaseUri = "http://localhost",
                        RevocationRegistrySize = 2,
                        Tag = "revoc"
                    });

            NonRevocableCredentialDefinitionId = await PairedAgents.Agent1.Provider.GetRequiredService<ISchemaService>()
                .CreateCredentialDefinitionAsync(
                    context: IssuerAgentContext,
                    new CredentialDefinitionConfiguration
                    {
                        SchemaId = _credentialSchemaId,
                        EnableRevocation = false,
                        RevocationRegistryBaseUri = "http://localhost",
                        Tag = "norevoc"
                    });
        }

        public override async Task DisposeAsync()
        {
            await base.DisposeAsync();

            foreach (var credentialRecord in await HolderCredentialService.ListAsync(HolderAgentContext))
                await HolderCredentialService.DeleteCredentialAsync(
                    HolderAgentContext, credentialRecord.Id);

            foreach (var credentialRecord in await IssuerCredentialService.ListAsync(IssuerAgentContext))
                await IssuerCredentialService.DeleteCredentialAsync(
                    IssuerAgentContext, credentialRecord.Id);

            await PairedAgents.Agent1.DisposeAsync();
            await PairedAgents.Agent2.DisposeAsync();

        }

        [Fact(DisplayName = "Test credential revocation")]
        public async Task CanRevokeCredential()
        {
            var receivedRevocationNotificationMessage = false;
            var receivedRevocationNotificationAckMessage = false;

            EventAggregator.GetEventByType<ServiceMessageProcessingEvent>()
                .Where(x => x.MessageType == MessageTypesHttps.RevocationNotification)
                .Subscribe(_ => receivedRevocationNotificationMessage = true);

            EventAggregator.GetEventByType<ServiceMessageProcessingEvent>()
                .Where(x => x.MessageType == MessageTypesHttps.RevocationNotification)
                .Subscribe(_ => receivedRevocationNotificationAckMessage = true);


            var (offer, record) = await IssuerCredentialService
                .CreateOfferAsync(IssuerAgentContext, new OfferConfiguration
                {
                    CredentialDefinitionId = RevocableCredentialDefinitionId,
                    IssuerDid = IssuerConfiguration.IssuerDid,
                    CredentialAttributeValues = new[]
                    {
                        new CredentialPreviewAttribute("name", "random"),
                        new CredentialPreviewAttribute("age", "22")
                    }
                });
            await IssuerMessageService.SendAsync(IssuerAgentContext, offer, PairedAgents.Connection1);

            var credentialRecordOnHolderSide = (await HolderCredentialService.ListAsync(HolderAgentContext))
                .First(credentialRecord => credentialRecord.State == CredentialState.Offered);
            var (request, _) = await HolderCredentialService.CreateRequestAsync(HolderAgentContext, credentialRecordOnHolderSide.Id);
            await HolderMessageService.SendAsync(HolderAgentContext, request, PairedAgents.Connection2);

            var credentialRecordOnIssuerSide = (await IssuerCredentialService.ListRequestsAsync(
                IssuerAgentContext)).First();
            var (issue, _) = await IssuerCredentialService.CreateCredentialAsync(IssuerAgentContext, credentialRecordOnIssuerSide.Id);
            await IssuerMessageService.SendAsync(IssuerAgentContext, issue, PairedAgents.Connection1);

            credentialRecordOnHolderSide =
                await HolderCredentialService.GetAsync(HolderAgentContext,
                    credentialRecordOnHolderSide.Id);
            credentialRecordOnIssuerSide =
                await IssuerCredentialService.GetAsync(IssuerAgentContext,
                    credentialRecordOnIssuerSide.Id);

            Assert.Equal(CredentialState.Issued, credentialRecordOnHolderSide.State);
            Assert.Equal(CredentialState.Issued, credentialRecordOnIssuerSide.State);

            await IssuerCredentialService.RevokeCredentialAsync(
                IssuerAgentContext, credentialRecordOnIssuerSide.Id, true);

            Assert.True(
                await HolderProofService.IsRevokedAsync(
                    HolderAgentContext,
                    credentialRecordOnHolderSide.Id));
            Assert.True(
                await IssuerProofService.IsRevokedAsync(
                    IssuerAgentContext,
                    credentialRecordOnIssuerSide.Id));

            Assert.True(receivedRevocationNotificationMessage);
            Assert.True(receivedRevocationNotificationAckMessage);
        }

        [Fact(DisplayName = "Test creating a new RevocationRegistry if first is getting full.")]
        public async Task CanSwitchToNewRevocationRegistryIfFull()
        {
            var receivedRevocationNotificationMessage = false;
            var receivedRevocationNotificationAckMessage = false;

            EventAggregator.GetEventByType<ServiceMessageProcessingEvent>()
                .Where(x => x.MessageType == MessageTypesHttps.RevocationNotification)
                .Subscribe(_ => receivedRevocationNotificationMessage = true);

            EventAggregator.GetEventByType<ServiceMessageProcessingEvent>()
                .Where(x => x.MessageType == MessageTypesHttps.RevocationNotification)
                .Subscribe(_ => receivedRevocationNotificationAckMessage = true);

            //First Credential
            var (offer, record) = await IssuerCredentialService
                .CreateOfferAsync(IssuerAgentContext, new OfferConfiguration
                {
                    CredentialDefinitionId = RevocableCredentialDefinitionId,
                    IssuerDid = IssuerConfiguration.IssuerDid,
                    CredentialAttributeValues = new[]
                    {
                        new CredentialPreviewAttribute("name", "random"),
                        new CredentialPreviewAttribute("age", "22")
                    }
                });
            await IssuerMessageService.SendAsync(IssuerAgentContext, offer, PairedAgents.Connection1);

            var credentialRecordOnHolderSide = (await HolderCredentialService.ListAsync(HolderAgentContext))
                .First(credentialRecord => credentialRecord.State == CredentialState.Offered);
            var (request, _) = await HolderCredentialService.CreateRequestAsync(HolderAgentContext, credentialRecordOnHolderSide.Id);
            await HolderMessageService.SendAsync(HolderAgentContext, request, PairedAgents.Connection2);

            var credentialRecordOnIssuerSide = (await IssuerCredentialService.ListRequestsAsync(
                IssuerAgentContext)).First();
            var (issue, _) = await IssuerCredentialService.CreateCredentialAsync(IssuerAgentContext, credentialRecordOnIssuerSide.Id);
            await IssuerMessageService.SendAsync(IssuerAgentContext, issue, PairedAgents.Connection1);

            credentialRecordOnHolderSide =
                await HolderCredentialService.GetAsync(HolderAgentContext,
                    credentialRecordOnHolderSide.Id);
            credentialRecordOnIssuerSide =
                await IssuerCredentialService.GetAsync(IssuerAgentContext,
                    credentialRecordOnIssuerSide.Id);

            Assert.Equal(CredentialState.Issued, credentialRecordOnHolderSide.State);
            Assert.Equal(CredentialState.Issued, credentialRecordOnIssuerSide.State);

            //Second Credential
            var (offer2, record2) = await IssuerCredentialService
                .CreateOfferAsync(IssuerAgentContext, new OfferConfiguration
                {
                    CredentialDefinitionId = RevocableCredentialDefinitionId,
                    IssuerDid = IssuerConfiguration.IssuerDid,
                    CredentialAttributeValues = new[]
                    {
                        new CredentialPreviewAttribute("name", "random"),
                        new CredentialPreviewAttribute("age", "22")
                    }
                });
            await IssuerMessageService.SendAsync(IssuerAgentContext, offer2, PairedAgents.Connection1);

            var credentialRecordOnHolderSide2 = (await HolderCredentialService.ListAsync(HolderAgentContext))
                .First(credentialRecord => credentialRecord.State == CredentialState.Offered);
            var (request2, _) = await HolderCredentialService.CreateRequestAsync(HolderAgentContext, credentialRecordOnHolderSide2.Id);
            await HolderMessageService.SendAsync(HolderAgentContext, request2, PairedAgents.Connection2);

            var credentialRecordOnIssuerSide2 = (await IssuerCredentialService.ListRequestsAsync(
                IssuerAgentContext)).First();
            var (issue2, _) = await IssuerCredentialService.CreateCredentialAsync(IssuerAgentContext, credentialRecordOnIssuerSide2.Id);
            await IssuerMessageService.SendAsync(IssuerAgentContext, issue2, PairedAgents.Connection1);

            credentialRecordOnHolderSide2 =
                await HolderCredentialService.GetAsync(HolderAgentContext,
                    credentialRecordOnHolderSide2.Id);
            credentialRecordOnIssuerSide2 =
                await IssuerCredentialService.GetAsync(IssuerAgentContext,
                    credentialRecordOnIssuerSide2.Id);

            Assert.Equal(CredentialState.Issued, credentialRecordOnHolderSide2.State);
            Assert.Equal(CredentialState.Issued, credentialRecordOnIssuerSide2.State);

            //Third Credential
            var (offer3, record3) = await IssuerCredentialService
                .CreateOfferAsync(IssuerAgentContext, new OfferConfiguration
                {
                    CredentialDefinitionId = RevocableCredentialDefinitionId,
                    IssuerDid = IssuerConfiguration.IssuerDid,
                    CredentialAttributeValues = new[]
                    {
                        new CredentialPreviewAttribute("name", "random"),
                        new CredentialPreviewAttribute("age", "22")
                    }
                });
            await IssuerMessageService.SendAsync(IssuerAgentContext, offer3, PairedAgents.Connection1);

            var credentialRecordOnHolderSide3 = (await HolderCredentialService.ListAsync(HolderAgentContext))
                .First(credentialRecord => credentialRecord.State == CredentialState.Offered);
            var (request3, _) = await HolderCredentialService.CreateRequestAsync(HolderAgentContext, credentialRecordOnHolderSide3.Id);
            await HolderMessageService.SendAsync(HolderAgentContext, request3, PairedAgents.Connection2);

            var credentialRecordOnIssuerSide3 = (await IssuerCredentialService.ListRequestsAsync(
                IssuerAgentContext)).First();
            var (issue3, _) = await IssuerCredentialService.CreateCredentialAsync(IssuerAgentContext, credentialRecordOnIssuerSide3.Id);
            await IssuerMessageService.SendAsync(IssuerAgentContext, issue3, PairedAgents.Connection1);

            credentialRecordOnHolderSide3 =
                await HolderCredentialService.GetAsync(HolderAgentContext,
                    credentialRecordOnHolderSide3.Id);
            credentialRecordOnIssuerSide3 =
                await IssuerCredentialService.GetAsync(IssuerAgentContext,
                    credentialRecordOnIssuerSide3.Id);

            Assert.Equal(CredentialState.Issued, credentialRecordOnHolderSide3.State);
            Assert.Equal(CredentialState.Issued, credentialRecordOnIssuerSide3.State);

            //Assert that third credential has other RevocationRegistryId than first and second credential
            Assert.Equal(credentialRecordOnIssuerSide.RevocationRegistryId, credentialRecordOnIssuerSide2.RevocationRegistryId);
            Assert.Equal(credentialRecordOnHolderSide.RevocationRegistryId, credentialRecordOnHolderSide2.RevocationRegistryId);
            Assert.NotEqual(credentialRecordOnIssuerSide3.RevocationRegistryId, credentialRecordOnIssuerSide2.RevocationRegistryId);
            Assert.NotEqual(credentialRecordOnHolderSide3.RevocationRegistryId, credentialRecordOnHolderSide2.RevocationRegistryId);

            //Assert that the credentials have the right credential revocation index
            Assert.Equal("1", credentialRecordOnIssuerSide.CredentialRevocationId); // first revocationRegistry
            Assert.Equal("2", credentialRecordOnIssuerSide2.CredentialRevocationId);// first revocationRegistry
            Assert.Equal("1", credentialRecordOnIssuerSide3.CredentialRevocationId);// second revocationRegistry
            Assert.Null(credentialRecordOnHolderSide.CredentialRevocationId);
            Assert.Null(credentialRecordOnHolderSide2.CredentialRevocationId);
            Assert.Null(credentialRecordOnHolderSide3.CredentialRevocationId);
        }

        [Fact(DisplayName = "Test revoking two credentials in one revocation registry and verify that they are listed in the revocation record.")]
        public async Task VerifyThatRevokedCredentialsAreListedInRevocationRecord()
        {
            var receivedRevocationNotificationMessage = false;
            var receivedRevocationNotificationAckMessage = false;

            EventAggregator.GetEventByType<ServiceMessageProcessingEvent>()
                .Where(x => x.MessageType == MessageTypesHttps.RevocationNotification)
                .Subscribe(_ => receivedRevocationNotificationMessage = true);

            EventAggregator.GetEventByType<ServiceMessageProcessingEvent>()
                .Where(x => x.MessageType == MessageTypesHttps.RevocationNotification)
                .Subscribe(_ => receivedRevocationNotificationAckMessage = true);

            //First Credential
            var (offer, record) = await IssuerCredentialService
                .CreateOfferAsync(IssuerAgentContext, new OfferConfiguration
                {
                    CredentialDefinitionId = RevocableCredentialDefinitionId,
                    IssuerDid = IssuerConfiguration.IssuerDid,
                    CredentialAttributeValues = new[]
                    {
                        new CredentialPreviewAttribute("name", "random"),
                        new CredentialPreviewAttribute("age", "22")
                    }
                });
            await IssuerMessageService.SendAsync(IssuerAgentContext, offer, PairedAgents.Connection1);

            var credentialRecordOnHolderSide = (await HolderCredentialService.ListAsync(HolderAgentContext))
                .First(credentialRecord => credentialRecord.State == CredentialState.Offered);
            var (request, _) = await HolderCredentialService.CreateRequestAsync(HolderAgentContext, credentialRecordOnHolderSide.Id);
            await HolderMessageService.SendAsync(HolderAgentContext, request, PairedAgents.Connection2);

            var credentialRecordOnIssuerSide = (await IssuerCredentialService.ListRequestsAsync(
                IssuerAgentContext)).First();
            var (issue, _) = await IssuerCredentialService.CreateCredentialAsync(IssuerAgentContext, credentialRecordOnIssuerSide.Id);
            await IssuerMessageService.SendAsync(IssuerAgentContext, issue, PairedAgents.Connection1);

            credentialRecordOnHolderSide =
                await HolderCredentialService.GetAsync(HolderAgentContext,
                    credentialRecordOnHolderSide.Id);
            credentialRecordOnIssuerSide =
                await IssuerCredentialService.GetAsync(IssuerAgentContext,
                    credentialRecordOnIssuerSide.Id);

            Assert.Equal(CredentialState.Issued, credentialRecordOnHolderSide.State);
            Assert.Equal(CredentialState.Issued, credentialRecordOnIssuerSide.State);

            //Revoke first credential
            await IssuerCredentialService.RevokeCredentialAsync(
                IssuerAgentContext, credentialRecordOnIssuerSide.Id, true);

            Assert.True(
                await HolderProofService.IsRevokedAsync(
                    HolderAgentContext,
                    credentialRecordOnHolderSide.Id));
            Assert.True(
                await IssuerProofService.IsRevokedAsync(
                    IssuerAgentContext,
                    credentialRecordOnIssuerSide.Id));

            Assert.True(receivedRevocationNotificationMessage);
            Assert.True(receivedRevocationNotificationAckMessage);

            //Second Credential
            var (offer2, record2) = await IssuerCredentialService
                .CreateOfferAsync(IssuerAgentContext, new OfferConfiguration
                {
                    CredentialDefinitionId = RevocableCredentialDefinitionId,
                    IssuerDid = IssuerConfiguration.IssuerDid,
                    CredentialAttributeValues = new[]
                    {
                        new CredentialPreviewAttribute("name", "random"),
                        new CredentialPreviewAttribute("age", "22")
                    }
                });
            await IssuerMessageService.SendAsync(IssuerAgentContext, offer2, PairedAgents.Connection1);

            var credentialRecordOnHolderSide2 = (await HolderCredentialService.ListAsync(HolderAgentContext))
                .First(credentialRecord => credentialRecord.State == CredentialState.Offered);
            var (request2, _) = await HolderCredentialService.CreateRequestAsync(HolderAgentContext, credentialRecordOnHolderSide2.Id);
            await HolderMessageService.SendAsync(HolderAgentContext, request2, PairedAgents.Connection2);

            var credentialRecordOnIssuerSide2 = (await IssuerCredentialService.ListRequestsAsync(
                IssuerAgentContext)).First();
            var (issue2, _) = await IssuerCredentialService.CreateCredentialAsync(IssuerAgentContext, credentialRecordOnIssuerSide2.Id);
            await IssuerMessageService.SendAsync(IssuerAgentContext, issue2, PairedAgents.Connection1);

            credentialRecordOnHolderSide2 =
                await HolderCredentialService.GetAsync(HolderAgentContext,
                    credentialRecordOnHolderSide2.Id);
            credentialRecordOnIssuerSide2 =
                await IssuerCredentialService.GetAsync(IssuerAgentContext,
                    credentialRecordOnIssuerSide2.Id);

            Assert.Equal(CredentialState.Issued, credentialRecordOnHolderSide2.State);
            Assert.Equal(CredentialState.Issued, credentialRecordOnIssuerSide2.State);

            //Revoke second credential
            await IssuerCredentialService.RevokeCredentialAsync(
                IssuerAgentContext, credentialRecordOnIssuerSide2.Id, true);

            Assert.True(
                await HolderProofService.IsRevokedAsync(
                    HolderAgentContext,
                    credentialRecordOnHolderSide2.Id));
            Assert.True(
                await IssuerProofService.IsRevokedAsync(
                    IssuerAgentContext,
                    credentialRecordOnIssuerSide2.Id));

            Assert.True(receivedRevocationNotificationMessage);
            Assert.True(receivedRevocationNotificationAckMessage);

            var revocationRecordIssuerSide = await RecordService.GetAsync<RevocationRegistryRecord>(IssuerAgentContext.AriesStorage, credentialRecordOnIssuerSide.RevocationRegistryId);

            //Assert that both credentials are on the index used list in RevocationRecord on issuer side
            Assert.Contains(long.Parse(credentialRecordOnIssuerSide.CredentialRevocationId), revocationRecordIssuerSide.CredRevocationIdxUsed);
            Assert.Contains(long.Parse(credentialRecordOnIssuerSide2.CredentialRevocationId), revocationRecordIssuerSide.CredRevocationIdxUsed);
        }

        [Fact(DisplayName = "Test verification without revocation")]
        public async Task CanVerifyWithoutRevocation()
        {
            var (offer, record) = await IssuerCredentialService
                .CreateOfferAsync(IssuerAgentContext, new OfferConfiguration
                {
                    CredentialDefinitionId = NonRevocableCredentialDefinitionId,
                    IssuerDid = IssuerConfiguration.IssuerDid,
                    CredentialAttributeValues = new[]
                    {
                        new CredentialPreviewAttribute("name", "random"),
                        new CredentialPreviewAttribute("age", "22")
                    }
                });
            await IssuerMessageService
                .SendAsync(IssuerAgentContext, offer, PairedAgents.Connection1);

            var credentialRecordOnHolderSide = (await HolderCredentialService.ListAsync(HolderAgentContext))
                .First(credentialRecord => credentialRecord.State == CredentialState.Offered);
            var (request, _) = await HolderCredentialService.CreateRequestAsync(HolderAgentContext, credentialRecordOnHolderSide.Id);
            await HolderMessageService.SendAsync(HolderAgentContext, request, PairedAgents.Connection2);

            var credentialRecordOnIssuerSide = (await IssuerCredentialService.ListRequestsAsync(
                IssuerAgentContext)).First();
            var (issue, _) = await IssuerCredentialService.CreateCredentialAsync(IssuerAgentContext, credentialRecordOnIssuerSide.Id);
            await IssuerMessageService.SendAsync(IssuerAgentContext, issue, PairedAgents.Connection1);

            credentialRecordOnHolderSide =
                await HolderCredentialService.GetAsync(HolderAgentContext,
                    credentialRecordOnHolderSide.Id);
            credentialRecordOnIssuerSide =
                await IssuerCredentialService.GetAsync(IssuerAgentContext,
                    credentialRecordOnIssuerSide.Id);

            Assert.Equal(CredentialState.Issued, credentialRecordOnHolderSide.State);
            Assert.Equal(CredentialState.Issued, credentialRecordOnIssuerSide.State);

            var (requestPresentationMessage, proofRecordIssuer) = await IssuerProofService
                .CreateRequestAsync(IssuerAgentContext, new ProofRequest
                {
                    Name = "Test Verification",
                    Version = "1.0",
                    Nonce = await SharedRsPresReq.GenerateNonceAsync(),
                    RequestedAttributes = new Dictionary<string, ProofAttributeInfo>
                    {
                        { "id-verification", new ProofAttributeInfo { Names = new [] { "name", "age" } } }
                    }
                });

            var proofRecordHolder = await HolderProofService.ProcessRequestAsync(HolderAgentContext, requestPresentationMessage, PairedAgents.Connection2);
            var availableCredentials = await HolderProofService.ListCredentialsForProofRequestAsync(HolderAgentContext, proofRecordHolder.RequestJson.ToObject<ProofRequest>(), "id-verification");

            var (presentationMessage, _) = await HolderProofService.CreatePresentationAsync(
                HolderAgentContext, proofRecordHolder.Id, new RequestedCredentials
                {
                    RequestedAttributes = new Dictionary<string, RequestedAttribute>
                    {
                        { "id-verification", new RequestedAttribute
                            {
                                CredentialId = availableCredentials.First().CredentialInfo.Referent,
                                Revealed = true
                            }
                        }
                    }
                });

            proofRecordIssuer = await IssuerProofService.ProcessPresentationAsync(IssuerAgentContext, presentationMessage);
            var valid = await IssuerProofService.VerifyProofAsync(IssuerAgentContext, proofRecordIssuer.Id);

            Assert.True(valid);
            Assert.False(await HolderProofService.IsRevokedAsync(HolderAgentContext, availableCredentials.First().CredentialInfo.Referent));
        }

        [Fact(DisplayName = "Test verification with NonRevoked set on proof request level")]
        public async Task CanVerifyWithNonRevokedSetOnProofRequestLevel()
        {
            var (offer, record) = await IssuerCredentialService
                .CreateOfferAsync(IssuerAgentContext, new OfferConfiguration
                {
                    CredentialDefinitionId = RevocableCredentialDefinitionId,
                    IssuerDid = IssuerConfiguration.IssuerDid,
                    CredentialAttributeValues = new[]
                    {
                        new CredentialPreviewAttribute("name", "random"),
                        new CredentialPreviewAttribute("age", "22")
                    }
                });
            await IssuerMessageService.SendAsync(IssuerAgentContext, offer, PairedAgents.Connection1);

            var credentialRecordOnHolderSide = (await HolderCredentialService.ListAsync(HolderAgentContext))
                .First(credentialRecord => credentialRecord.State == CredentialState.Offered);
            var (request, _) = await HolderCredentialService.CreateRequestAsync(HolderAgentContext, credentialRecordOnHolderSide.Id);
            await HolderMessageService.SendAsync(HolderAgentContext, request, PairedAgents.Connection2);

            var credentialRecordOnIssuerSide = (await IssuerCredentialService.ListRequestsAsync(
                IssuerAgentContext)).First();
            var (issuance, _) = await IssuerCredentialService.CreateCredentialAsync(IssuerAgentContext, credentialRecordOnIssuerSide.Id);
            await IssuerMessageService.SendAsync(IssuerAgentContext, issuance, PairedAgents.Connection1);

            credentialRecordOnHolderSide =
                await HolderCredentialService.GetAsync(HolderAgentContext,
                    credentialRecordOnHolderSide.Id);
            credentialRecordOnIssuerSide =
                await IssuerCredentialService.GetAsync(IssuerAgentContext,
                    credentialRecordOnIssuerSide.Id);

            Assert.Equal(CredentialState.Issued, credentialRecordOnHolderSide.State);
            Assert.Equal(CredentialState.Issued, credentialRecordOnIssuerSide.State);

            var (requestPresentationMessage, proofRecordIssuer) = await IssuerProofService
                .CreateRequestAsync(IssuerAgentContext, new ProofRequest
                {
                    Name = "Test Verification",
                    Version = "1.0",
                    Nonce = await SharedRsPresReq.GenerateNonceAsync(),
                    RequestedAttributes = new Dictionary<string, ProofAttributeInfo>
                    {
                        { "id-verification", new ProofAttributeInfo { Names = new [] { "name", "age" } } }
                    },
                    NonRevoked = new RevocationInterval
                    {
                        From = 0,
                        To = _now
                    }
                });

            var proofRecordHolder = await HolderProofService.ProcessRequestAsync(HolderAgentContext, requestPresentationMessage, PairedAgents.Connection2);
            var availableCredentials = await HolderProofService
                .ListCredentialsForProofRequestAsync(HolderAgentContext, proofRecordHolder.RequestJson.ToObject<ProofRequest>(), "id-verification");

            var (presentationMessage, _) = await HolderProofService.CreatePresentationAsync(
                HolderAgentContext, proofRecordHolder.Id, new RequestedCredentials
                {
                    RequestedAttributes = new Dictionary<string, RequestedAttribute>
                    {
                        { "id-verification", new RequestedAttribute
                            {
                                CredentialId = availableCredentials.First().CredentialInfo.Referent,
                                Revealed = true
                            }
                        }
                    }
                });
            proofRecordIssuer = await IssuerProofService.ProcessPresentationAsync(IssuerAgentContext, presentationMessage);

            var valid = await IssuerProofService.VerifyProofAsync(IssuerAgentContext, proofRecordIssuer.Id);
            Assert.True(valid);
        }

        [Fact(DisplayName = "Test verification with NonRevoked set on attribute level")]
        public async Task CanVerifyWithNonRevokedSetOnAttributeLevel()
        {
            var (offer, record) = await IssuerCredentialService
                .CreateOfferAsync(IssuerAgentContext, new OfferConfiguration
                {
                    CredentialDefinitionId = RevocableCredentialDefinitionId,
                    IssuerDid = IssuerConfiguration.IssuerDid,
                    CredentialAttributeValues = new[]
                    {
                        new CredentialPreviewAttribute("name", "random"),
                        new CredentialPreviewAttribute("age", "22")
                    }
                });
            await IssuerMessageService.SendAsync(IssuerAgentContext, offer, PairedAgents.Connection1);

            var credentialRecordOnHolderSide = (await HolderCredentialService.ListAsync(HolderAgentContext))
                .First(credentialRecord => credentialRecord.State == CredentialState.Offered);
            var (request, _) = await HolderCredentialService.CreateRequestAsync(HolderAgentContext, credentialRecordOnHolderSide.Id);
            await HolderMessageService.SendAsync(HolderAgentContext, request, PairedAgents.Connection2);

            var credentialRecordOnIssuerSide = (await IssuerCredentialService.ListRequestsAsync(
                IssuerAgentContext)).First();
            var (issuance, _) = await IssuerCredentialService.CreateCredentialAsync(IssuerAgentContext, credentialRecordOnIssuerSide.Id);
            await IssuerMessageService.SendAsync(IssuerAgentContext, issuance, PairedAgents.Connection1);

            credentialRecordOnHolderSide =
                await HolderCredentialService.GetAsync(HolderAgentContext,
                    credentialRecordOnHolderSide.Id);
            credentialRecordOnIssuerSide =
                await IssuerCredentialService.GetAsync(IssuerAgentContext,
                    credentialRecordOnIssuerSide.Id);

            Assert.Equal(CredentialState.Issued, credentialRecordOnHolderSide.State);
            Assert.Equal(CredentialState.Issued, credentialRecordOnIssuerSide.State);

            var (requestPresentationMessage, proofRecordIssuer) = await IssuerProofService
                .CreateRequestAsync(IssuerAgentContext, new ProofRequest
                {
                    Name = "Test Verification",
                    Version = "1.0",
                    Nonce = await SharedRsPresReq.GenerateNonceAsync(),
                    RequestedAttributes = new Dictionary<string, ProofAttributeInfo>
                    {
                        { "id-verification", new ProofAttributeInfo
                            {
                                Names = new [] { "name", "age" },
                                NonRevoked = new RevocationInterval
                                {
                                    From = 0,
                                    To = _now
                                }
                            }
                        }
                    }
                });

            var proofRecordHolder = await HolderProofService.ProcessRequestAsync(HolderAgentContext, requestPresentationMessage, PairedAgents.Connection2);
            var availableCredentials = await HolderProofService
                .ListCredentialsForProofRequestAsync(HolderAgentContext, proofRecordHolder.RequestJson.ToObject<ProofRequest>(), "id-verification");

            var (presentationMessage, _) = await HolderProofService.CreatePresentationAsync(
                HolderAgentContext, proofRecordHolder.Id, new RequestedCredentials
                {
                    RequestedAttributes = new Dictionary<string, RequestedAttribute>
                    {
                        { "id-verification", new RequestedAttribute
                            {
                                CredentialId = availableCredentials.First().CredentialInfo.Referent,
                                Revealed = true
                            }
                        }
                    }
                });

            proofRecordIssuer = await IssuerProofService.ProcessPresentationAsync(IssuerAgentContext, presentationMessage);

            var valid = await IssuerProofService.VerifyProofAsync(IssuerAgentContext, proofRecordIssuer.Id);
            Assert.True(valid);
        }

    }
}
