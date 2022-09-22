using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.Handshakes.Connection;
using Hyperledger.Aries.Features.Handshakes.Connection.Models;
using Hyperledger.Aries.Features.IssueCredential;
using Hyperledger.Aries.Features.PresentProof;
using Hyperledger.Aries.Ledger;
using Hyperledger.Aries.Models.Events;
using Hyperledger.Aries.Payments;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.TestHarness;
using Hyperledger.Indy.AnonCredsApi;
using Hyperledger.Indy.PoolApi;
using Hyperledger.Indy.WalletApi;
using Hyperledger.TestHarness;
using Hyperledger.TestHarness.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using Xunit;
using IndySharedRsPresReq = indy_shared_rs_dotnet.IndyCredx.PresentationRequestApi;

namespace Hyperledger.Aries.Tests.Protocols
{
    [Trait("Category", "DefaultV1")]
    public class ProofTestsV1 : IAsyncLifetime
    {
        static ProofTestsV1()
        {
            global::Hyperledger.Aries.Utils.Runtime.SetFlags(Hyperledger.Aries.Utils.Runtime.LedgerLookupRetryFlag);
        }

        private readonly string IssuerConfig = $"{{\"id\":\"{Guid.NewGuid()}\"}}";
        private readonly string HolderConfig = $"{{\"id\":\"{Guid.NewGuid()}\"}}";
        private readonly string RequestorConfig = $"{{\"id\":\"{Guid.NewGuid()}\"}}";
        private const string WalletCredentials = "{\"key\":\"test_wallet_key\"}";

        private IAgentContext _issuerWallet;
        private IAgentContext _holderWallet;
        private IAgentContext _requestorWallet;

        private readonly IEventAggregator _eventAggregator;
        private readonly IConnectionService _connectionService;
        private readonly ICredentialService _credentialService;
        private readonly IProofService _proofService;
        private readonly IWalletRecordService _recordService;

        private readonly ISchemaService _schemaService;

        private readonly ConcurrentBag<AgentMessage> _messages = new ConcurrentBag<AgentMessage>();

        public ProofTestsV1()
        {
            _recordService = new DefaultWalletRecordService();
            var ledgerService = new DefaultLedgerService(new DefaultLedgerSigningService(new DefaultProvisioningService(_recordService, new DefaultWalletService(), Options.Create(new AgentOptions()))));

            _eventAggregator = new EventAggregator();

            var messageService = new DefaultMessageService(new Mock<ILogger<DefaultMessageService>>().Object, new IMessageDispatcher[] { });

            var provisioning = ServiceUtils.GetDefaultMockProvisioningService();
            var paymentService = new DefaultPaymentService();

            var clientFactory = new Mock<IHttpClientFactory>();
            clientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient());

            var tailsService = new DefaultTailsService(ledgerService, Options.Create(new Configuration.AgentOptions()), clientFactory.Object);
            _schemaService = new DefaultSchemaService(provisioning, _recordService, ledgerService, paymentService, tailsService, Options.Create(new Configuration.AgentOptions()));

            _connectionService = new DefaultConnectionService(
                _eventAggregator,
                _recordService,
                provisioning,
                new Mock<ILogger<DefaultConnectionService>>().Object);

            _credentialService = new DefaultCredentialService(
                _eventAggregator,
                ledgerService,
                _connectionService,
                _recordService,
                _schemaService,
                tailsService,
                provisioning,
                paymentService,
                messageService,
                new Mock<ILogger<DefaultCredentialService>>().Object);

            _proofService = new DefaultProofService(
                _eventAggregator,
                _connectionService,
                _recordService,
                provisioning,
                ledgerService,
                tailsService,
                messageService,
                new Mock<ILogger<DefaultProofService>>().Object);
        }

        public async Task InitializeAsync()
        {
            _issuerWallet = await AgentUtils.Create(IssuerConfig, WalletCredentials, true);
            _holderWallet = await AgentUtils.Create(HolderConfig, WalletCredentials, true);
            _requestorWallet = await AgentUtils.Create(RequestorConfig, WalletCredentials, true);
        }

        [Fact]
        public async Task RequestorInitiatedCredentialProofDemo()
        {
            var events = 0;
            _eventAggregator.GetEventByType<ServiceMessageProcessingEvent>()
                .Where(_ => (_.MessageType == MessageTypes.PresentProofNames.RequestPresentation ||
                             _.MessageType == MessageTypes.PresentProofNames.Presentation))
                .Subscribe(_ =>
                {
                    events++;
                });

            //Setup a connection and issue the credentials to the holder
            var (issuerConnection, holderConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _issuerWallet, _holderWallet);

            await Scenarios.IssueCredentialAsync(
                _recordService, _schemaService, _credentialService, _messages, issuerConnection,
                holderConnection, _issuerWallet, _holderWallet, TestConstants.DefaultMasterSecret, new List<CredentialPreviewAttribute>
                {
                    new CredentialPreviewAttribute("first_name", "Test"),
                    new CredentialPreviewAttribute("last_name", "Holder")
                });

            _messages.Clear();

            //Requestor initialize a connection with the holder
            var (holderRequestorConnection, requestorConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _holderWallet, _requestorWallet);

            await Scenarios.RequestorInitiatedProofProtocolAsync(_proofService, _messages, holderRequestorConnection, requestorConnection,
                _holderWallet, _requestorWallet, new ProofRequest()
                {
                    Name = "ProofReq",
                    Version = "1.0",
                    Nonce = await AnonCreds.GenerateNonceAsync(),
                    RequestedAttributes = new Dictionary<string, ProofAttributeInfo>
                    {
                        {"first-name-requirement", new ProofAttributeInfo {Name = "first_name"}}
                    }
                });

            _messages.Clear();

            Assert.True(events == 2);
        }

        [Fact]
        public async Task ProposerInitiatedCredentialProofDemo()
        {
                    var events = 0;
            _eventAggregator.GetEventByType<ServiceMessageProcessingEvent>()
                .Where(_ => (_.MessageType == MessageTypes.PresentProofNames.ProposePresentation ||
                             _.MessageType == MessageTypes.PresentProofNames.RequestPresentation ||
                             _.MessageType == MessageTypes.PresentProofNames.Presentation))
                .Subscribe(_ =>
                {
                    events++;
                });

            //Setup a connection and issue the credentials to the holder
            var (issuerConnection, holderConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _issuerWallet, _holderWallet);

            var (issuerCredential, holderCredential) = await Scenarios.IssueCredentialAsync(
                _recordService, _schemaService, _credentialService, _messages, issuerConnection,
                holderConnection, _issuerWallet, _holderWallet, TestConstants.DefaultMasterSecret, new List<CredentialPreviewAttribute>
                {
                    new CredentialPreviewAttribute("first_name", "Test"),
                    new CredentialPreviewAttribute("last_name", "Holder")
                });

            _messages.Clear();

            //Requestor initialize a connection with the holder
            var (holderRequestorConnection, requestorConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _holderWallet, _requestorWallet);

            var (holderProofRecord, requestorProofRecord) = await Scenarios.ProposerInitiatedProofProtocolAsync(_proofService, _messages, holderRequestorConnection, requestorConnection,
                _holderWallet, _requestorWallet, new ProofProposal()
                {
                    Comment = "Hello, World",
                    ProposedAttributes = new List<ProposedAttribute>
                    {
                        new ProposedAttribute 
                        {
                            Name = "first_name",
                            CredentialDefinitionId = holderCredential.CredentialDefinitionId,
                            Referent = "0",
                            Value = "Test"
                        }
                    }
                });

            _messages.Clear();

            Assert.True(events == 3);

        }

        [Fact]
        public async Task CreateProofRequestFromProposal()
        {
            var events = 0;
            _eventAggregator.GetEventByType<ServiceMessageProcessingEvent>()
                .Where(_ => (_.MessageType == MessageTypes.PresentProofNames.ProposePresentation ||
                             _.MessageType == MessageTypes.PresentProofNames.RequestPresentation ||
                             _.MessageType == MessageTypes.IssueCredentialNames.RequestCredential ||
                             _.MessageType == MessageTypes.IssueCredentialNames.IssueCredential))
                .Subscribe(_ =>
                {
                    events++;
                });

            // Setup secure connection between issuer and holder
            var (issuerConnection, holderConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _issuerWallet, _holderWallet);

            var (issuerCredential, holderCredential) = await Scenarios.IssueCredentialAsync(
                _recordService, _schemaService, _credentialService, _messages, issuerConnection,
                holderConnection, _issuerWallet, _holderWallet, TestConstants.DefaultMasterSecret, new List<CredentialPreviewAttribute>
                {
                     new CredentialPreviewAttribute("first_name", "Test"),
                     new CredentialPreviewAttribute("last_name", "Test"),
                     new CredentialPreviewAttribute("salary", "100000"),
                     new CredentialPreviewAttribute("age", "25"),
                     new CredentialPreviewAttribute("wellbeing", "100")
                });

           

            Assert.Equal(issuerCredential.State, holderCredential.State);
            Assert.Equal(CredentialState.Issued, issuerCredential.State);
            var (message, record) = await _proofService.CreateProposalAsync(_holderWallet, new ProofProposal
            {
                Comment = "Hello, World",
                ProposedAttributes = new List<ProposedAttribute>
                 {
                     new ProposedAttribute
                     {
                         Name = "first_name",
                         CredentialDefinitionId = holderCredential.CredentialDefinitionId,
                         Referent = "Proof of Name",
                         Value = "Joe"
                     },
                     new ProposedAttribute
                     {
                         Name = "last_name",
                         CredentialDefinitionId = holderCredential.CredentialDefinitionId,
                         Referent = "Proof of Name",
                         Value = "Shmoe"
                     },
                     new ProposedAttribute
                     {
                         Name = "age",
                         CredentialDefinitionId = holderCredential.CredentialDefinitionId,
                         Referent = "Proof of Age",
                         Value = "Shmoe"
                     }
                },
                ProposedPredicates = new List<ProposedPredicate>
                {
                    new ProposedPredicate
                    {
                        Name = "salary",
                        CredentialDefinitionId = holderCredential.CredentialDefinitionId,
                        Predicate = ">",
                        Threshold = 99999,
                        Referent = "Proof of Salary > $99,999"

                    },
                    new ProposedPredicate
                     {
                         Name = "wellbeing",
                         CredentialDefinitionId = holderCredential.CredentialDefinitionId,
                         Referent = "Proof of Wellbeing",
                         Predicate = "<",
                         Threshold = 99999
                     }
                }
            }, holderConnection.Id);
            Assert.NotNull(message);

            // Process Proposal 
            record = await _proofService.ProcessProposalAsync(_issuerWallet, message, issuerConnection);

            // 
            RequestPresentationMessage requestMessage;

            (requestMessage, record) = await _proofService.CreateRequestFromProposalAsync(_issuerWallet, new ProofRequestParameters
            {
                Name = "Test",
                Version = "1.0",
                NonRevoked = null
            }, record.Id, issuerConnection.Id);

            Assert.NotNull(requestMessage);
            Assert.NotNull(record);

            var actualProofRequest = record.RequestJson.ToObject<ProofRequest>();
            var expectedProofRequest = new ProofRequest
            {
                Name = "Test",
                Version = "1.0",
                Nonce = actualProofRequest.Nonce,
                RequestedAttributes = new Dictionary<string, ProofAttributeInfo>
                {
                    {
                        "Proof of Name",new ProofAttributeInfo
                        {
                            Name=null,
                            Names= new string[] {"first_name", "last_name" },
                            NonRevoked=null,
                            Restrictions=new List<AttributeFilter>
                            {
                                new AttributeFilter
                                {
                                    CredentialDefinitionId = holderCredential.CredentialDefinitionId
                                }
                            }
                        }
                    },
                    {
                        "Proof of Age", new ProofAttributeInfo
                        {
                            Name="age",
                            Names=null,
                            NonRevoked=null,
                            Restrictions=new List<AttributeFilter>
                            {
                                new AttributeFilter
                                {
                                    CredentialDefinitionId = holderCredential.CredentialDefinitionId
                                }
                            }
                        }
                    }
                },
                RequestedPredicates = new Dictionary<string, ProofPredicateInfo>
                {
                    {
                        "Proof of Salary > $99,999", new ProofPredicateInfo
                        {
                            Name = "salary",
                            Names = null,
                            NonRevoked = null,
                            PredicateType = ">",
                            PredicateValue = 99999,
                            Restrictions=new List<AttributeFilter>
                            {
                                new AttributeFilter
                                {
                                    CredentialDefinitionId = holderCredential.CredentialDefinitionId
                                }
                            }
                        }
                    },
                    {
                        "Proof of Wellbeing", new ProofPredicateInfo
                        {
                            Name = "wellbeing",
                            Names = null,
                            NonRevoked = null,
                            PredicateType = "<",
                            PredicateValue = 99999,
                            Restrictions=new List<AttributeFilter>
                            {
                                new AttributeFilter
                                {
                                    CredentialDefinitionId = holderCredential.CredentialDefinitionId
                                }
                            }
                        }
                    }
                }
            };   
            var expectedProofRecord = new ProofRecord
            {
                State = ProofState.Requested,
                RequestJson = expectedProofRequest.ToJson(),
            };
           
            actualProofRequest.Should().BeEquivalentTo(expectedProofRequest);
        }

        [Fact]
        public async Task SendProofProposalThrowsConnectionInvalidState()
        {
            var connectionId = Guid.NewGuid().ToString();

            await _connectionService.CreateInvitationAsync(_issuerWallet,
                new InviteConfiguration { ConnectionId = connectionId });

            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _proofService.CreateProposalAsync(_issuerWallet, new ProofProposal
            {
                Comment = "Hello, World",
                ProposedAttributes = new List<ProposedAttribute>
                {
                    new ProposedAttribute() 
                    {
                        Name = "first_name",
                        CredentialDefinitionId = "asdf",
                        Referent = "asdf",
                        Value = "Joe"
                    }
                }
            }, connectionId));

            Assert.True(ex.ErrorCode == ErrorCode.RecordInInvalidState);
        }

        [Fact]
        public async Task CreateProofProposalSuccesfully()
        {
            var proposedAttributes = new List<ProposedAttribute>
            {
                new ProposedAttribute
                {
                    Name = "first_name",
                    CredentialDefinitionId = "1",
                    SchemaId = "1",
                    IssuerDid ="1",
                    Referent = "Proof of Name",
                    Value = "Joe"
                },
                new ProposedAttribute
                {

                    Name = "second_name",
                    CredentialDefinitionId = "1",
                    SchemaId = "1",
                    IssuerDid ="1",
                    Referent = "Proof of Name",
                    Value = "Joe"
                },
                new ProposedAttribute
                {

                    Name = "age",
                    CredentialDefinitionId = "1",
                    SchemaId = "1",
                    IssuerDid ="1",
                    Referent = "Proof of Age",
                    Value = "Joe"
                }
            };
            var proposedPredicates =
            new List<ProposedPredicate>
            {
                new ProposedPredicate
                {
                    Name = "salary",
                    CredentialDefinitionId = "1",
                    Predicate = ">",
                    Threshold = 99999,
                    Referent = "Proof of Salary > $99,999"

                },
                new ProposedPredicate
                {
                    Name = "test",
                    CredentialDefinitionId = "1",
                    Predicate = ">",
                    Threshold = 99999,
                    Referent = "Proof of Test > $99,999"

                }

            };

            var (issuerConnection, holderConnection) = await Scenarios.EstablishConnectionAsync(
               _connectionService, _messages, _issuerWallet, _holderWallet);

            var proofProposal = new ProofProposal
            {
                Comment = "Hello, World",
                ProposedAttributes = proposedAttributes,
                ProposedPredicates = proposedPredicates
            };

            var (message, record) = await _proofService.CreateProposalAsync(_holderWallet, proofProposal, holderConnection.Id);

            var expectedMessage = new ProposePresentationMessage
            {
                Id = message.Id,
                Comment = "Hello, World",
                PresentationPreviewMessage = new PresentationPreviewMessage
                {
                    Id = message.PresentationPreviewMessage.Id,
                    ProposedAttributes = proposedAttributes.ToArray(),
                    ProposedPredicates = proposedPredicates.ToArray()
                }
            };

            message.Should().BeEquivalentTo(expectedMessage);

        }

        [Fact]
        public async Task CreateProofProposalThrowsInvalidParameterFormat()
        {
            // Setup secure connection between issuer and holder
            var (issuerConnection, holderConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _issuerWallet, _holderWallet);

            var proofProposal = new ProofProposal
            {
                Comment = "Hello, World",
                ProposedAttributes = new List<ProposedAttribute>
                 {
                     new ProposedAttribute
                     {
                         Name = "first_name",
                         CredentialDefinitionId = "1",
                         SchemaId = "1",
                         IssuerDid ="1",
                         Referent = "Proof of First Name",
                         Value = "Joe"
                     },
                     new ProposedAttribute
                     {

                         Name = "second_name",
                         CredentialDefinitionId = "2",
                         SchemaId = "2",
                         IssuerDid ="2",
                         Referent = "Proof of First Name",
                         Value = "Joe"
                     }
                 },
                ProposedPredicates = new List<ProposedPredicate>
                {
                    new ProposedPredicate
                    {
                        Name = "salary",
                        CredentialDefinitionId = "1",
                        Predicate = ">",
                        Threshold = 99999,
                        Referent = "Proof of Salary > $99,999"

                    }
                }
            };
      
            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () =>
                await _proofService.CreateProposalAsync(_holderWallet, proofProposal, holderConnection.Id));

            Assert.True(ex.ErrorCode == ErrorCode.InvalidParameterFormat);

            var len = proofProposal.ProposedAttributes.Count - 1;
            proofProposal.ProposedAttributes.Remove(proofProposal.ProposedAttributes[len]);
            proofProposal.ProposedPredicates.Add(new ProposedPredicate
            {
                Name = "name",
                CredentialDefinitionId = "2",
                Predicate = ">",
                Threshold = 99999,
                Referent = "Proof of Salary > $99,999"

            });

            ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () =>
              await _proofService.CreateProposalAsync(_holderWallet, proofProposal, holderConnection.Id));
            Assert.True(ex.ErrorCode == ErrorCode.InvalidParameterFormat);
            
        }

        [Fact]
        public async Task ProcessProofRecordNotFound()
        {
            var (issuerConnection, _) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _issuerWallet, _holderWallet);

            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () =>
                await _proofService.ProcessPresentationAsync(_issuerWallet, new PresentationMessage()));

            Assert.True(ex.ErrorCode == ErrorCode.RecordNotFound);
        }

        [Fact]
        public async Task ProcessProofInvalidState()
        {
            //Setup a connection and issue the credentials to the holder
            var (issuerConnection, holderConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _issuerWallet, _holderWallet);

            await Scenarios.IssueCredentialAsync(
                _recordService, _schemaService, _credentialService, _messages, issuerConnection,
                holderConnection, _issuerWallet, _holderWallet, TestConstants.DefaultMasterSecret, new List<CredentialPreviewAttribute>
                {
                    new CredentialPreviewAttribute("first_name", "Test"),
                    new CredentialPreviewAttribute("last_name", "Holder")
                });

            _messages.Clear();

            //Requestor initialize a connection with the holder
            var (_, requestorConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _holderWallet, _requestorWallet);

            // Verifier sends a proof request to prover
            {
                var proofRequestObject = new ProofRequest
                {
                    Name = "ProofReq",
                    Version = "1.0",
                    Nonce = await AnonCreds.GenerateNonceAsync(),
                    RequestedAttributes = new Dictionary<string, ProofAttributeInfo>
                    {
                        {"first-name-requirement", new ProofAttributeInfo {Name = "first_name"}}
                    }
                };

                //Requestor sends a proof request
                var (message, _) = await _proofService.CreateRequestAsync(_requestorWallet, proofRequestObject, requestorConnection.Id);
                _messages.Add(message);
            }

            // Holder accepts the proof requests and builds a proof
            {
                // Holder retrieves proof request message from their cloud agent
                var proofRequest = FindContentMessage<RequestPresentationMessage>();
                Assert.NotNull(proofRequest);

                // Holder stores the proof request
                var holderProofRequestId = await _proofService.ProcessRequestAsync(_holderWallet, proofRequest, holderConnection);
                var holderProofRecord = await _proofService.GetAsync(_holderWallet, holderProofRequestId.Id);
                var holderProofObject =
                    JsonConvert.DeserializeObject<ProofRequest>(holderProofRecord.RequestJson);

                var requestedCredentials = new RequestedCredentials();
                foreach (var requestedAttribute in holderProofObject.RequestedAttributes)
                {
                    var credentials =
                        await _proofService.ListCredentialsForProofRequestAsync(_holderWallet, holderProofObject,
                            requestedAttribute.Key);

                    requestedCredentials.RequestedAttributes.Add(requestedAttribute.Key,
                        new RequestedAttribute
                        {
                            CredentialId = credentials.First().CredentialInfo.Referent,
                            Revealed = true
                        });
                }

                foreach (var requestedAttribute in holderProofObject.RequestedPredicates)
                {
                    var credentials =
                        await _proofService.ListCredentialsForProofRequestAsync(_holderWallet, holderProofObject,
                            requestedAttribute.Key);

                    requestedCredentials.RequestedPredicates.Add(requestedAttribute.Key,
                        new RequestedAttribute
                        {
                            CredentialId = credentials.First().CredentialInfo.Referent,
                            Revealed = true
                        });
                }

                //Holder accepts the proof request and sends a proof
                (var proofMessage, var _) = await _proofService.CreatePresentationAsync(_holderWallet, holderProofRequestId.Id,
                    requestedCredentials);
                _messages.Add(proofMessage);
            }

            //Requestor retrives proof message from their cloud agent
            var proof = FindContentMessage<PresentationMessage>();
            Assert.NotNull(proof);

            //Requestor stores proof
            await _proofService.ProcessPresentationAsync(_requestorWallet, proof);

            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _proofService.ProcessPresentationAsync(_requestorWallet, proof));

            Assert.True(ex.ErrorCode == ErrorCode.RecordInInvalidState);
        }

        [Fact]
        public async Task AcceptProofRequestCredentialNotFound()
        {
            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _proofService.CreatePresentationAsync(_issuerWallet, "bad-proof-id", null));
            Assert.True(ex.ErrorCode == ErrorCode.RecordNotFound);
        }

        [Fact]
        public async Task AcceptProofRequestCredentialInvalidState()
        {
            //Setup a connection and issue the credentials to the holder
            var (issuerConnection, holderConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _issuerWallet, _holderWallet);

            await Scenarios.IssueCredentialAsync(
                _recordService, _schemaService, _credentialService, _messages, issuerConnection,
                holderConnection, _issuerWallet, _holderWallet, TestConstants.DefaultMasterSecret, new List<CredentialPreviewAttribute>
                {
                    new CredentialPreviewAttribute("first_name", "Test"),
                    new CredentialPreviewAttribute("last_name", "Holder")
                });

            _messages.Clear();

            //Requestor initialize a connection with the holder
            var (_, requestorConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _holderWallet, _requestorWallet);

            // Verifier sends a proof request to prover
            {
                var proofRequestObject = new ProofRequest
                {
                    Name = "ProofReq",
                    Version = "1.0",
                    Nonce = await AnonCreds.GenerateNonceAsync(),
                    RequestedAttributes = new Dictionary<string, ProofAttributeInfo>
                    {
                        {
                            "first-name-requirement",
                            new ProofAttributeInfo
                            {
                                Name = "first_name"
                            }
                        }
                    }
                };

                //Requestor sends a proof request
                var (message, _) = await _proofService.CreateRequestAsync(_requestorWallet, proofRequestObject, requestorConnection.Id);
                _messages.Add(message);
            }

            // Holder accepts the proof requests and builds a proof
            //Holder retrives proof request message from their cloud agent
            var proofRequest = FindContentMessage<RequestPresentationMessage>();
            Assert.NotNull(proofRequest);

            //Holder stores the proof request
            var holderProofRequestId = await _proofService.ProcessRequestAsync(_holderWallet, proofRequest, holderConnection);
            var holderProofRecord = await _proofService.GetAsync(_holderWallet, holderProofRequestId.Id);
            var holderProofObject =
                JsonConvert.DeserializeObject<ProofRequest>(holderProofRecord.RequestJson);

            var requestedCredentials = new RequestedCredentials();
            foreach (var requestedAttribute in holderProofObject.RequestedAttributes)
            {
                var credentials =
                    await _proofService.ListCredentialsForProofRequestAsync(_holderWallet, holderProofObject,
                        requestedAttribute.Key);

                requestedCredentials.RequestedAttributes.Add(requestedAttribute.Key,
                    new RequestedAttribute
                    {
                        CredentialId = credentials.First().CredentialInfo.Referent,
                        Revealed = true
                    });
            }

            foreach (var requestedAttribute in holderProofObject.RequestedPredicates)
            {
                var credentials =
                    await _proofService.ListCredentialsForProofRequestAsync(_holderWallet, holderProofObject,
                        requestedAttribute.Key);

                requestedCredentials.RequestedPredicates.Add(requestedAttribute.Key,
                    new RequestedAttribute
                    {
                        CredentialId = credentials.First().CredentialInfo.Referent,
                        Revealed = true
                    });
            }

            //Holder accepts the proof request and sends a proof
            await _proofService.CreatePresentationAsync(_holderWallet, holderProofRequestId.Id, requestedCredentials);
            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _proofService.CreatePresentationAsync(_holderWallet, holderProofRequestId.Id,
                requestedCredentials));

            Assert.True(ex.ErrorCode == ErrorCode.RecordInInvalidState);
        }

        [Fact]
        public async Task RejectProofRequestCredentialNotFound()
        {
            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _proofService.RejectProofRequestAsync(_issuerWallet, "bad-proof-id"));
            Assert.True(ex.ErrorCode == ErrorCode.RecordNotFound);
        }

        [Fact]
        public async Task RejectProofRequestCredentialInvalidState()
        {
            //Setup a connection and issue the credentials to the holder
            var (issuerConnection, holderConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _issuerWallet, _holderWallet);

            await Scenarios.IssueCredentialAsync(
                _recordService, _schemaService, _credentialService, _messages, issuerConnection,
                holderConnection, _issuerWallet, _holderWallet, TestConstants.DefaultMasterSecret, new List<CredentialPreviewAttribute>
                {
                    new CredentialPreviewAttribute("first_name", "Test"),
                    new CredentialPreviewAttribute("last_name", "Holder")
                });

            _messages.Clear();

            //Requestor initialize a connection with the holder
            var (_, requestorConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _holderWallet, _requestorWallet);

            // Verifier sends a proof request to prover
            {
                var proofRequestObject = new ProofRequest
                {
                    Name = "ProofReq",
                    Version = "1.0",
                    Nonce = await AnonCreds.GenerateNonceAsync(),
                    RequestedAttributes = new Dictionary<string, ProofAttributeInfo>
                    {
                        {"first-name-requirement", new ProofAttributeInfo {Name = "first_name"}}
                    }
                };

                //Requestor sends a proof request
                var (message, _) = await _proofService.CreateRequestAsync(_requestorWallet, proofRequestObject, requestorConnection.Id);
                _messages.Add(message);
            }

            //Holder retrieves proof request message from their cloud agent
            var proofRequest = FindContentMessage<RequestPresentationMessage>();
            Assert.NotNull(proofRequest);

            //Holder stores the proof request
            var holderProofRequestId = await _proofService.ProcessRequestAsync(_holderWallet, proofRequest, holderConnection);

            //Holder accepts the proof request and sends a proof
            await _proofService.RejectProofRequestAsync(_holderWallet, holderProofRequestId.Id);

            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _proofService.RejectProofRequestAsync(_holderWallet, holderProofRequestId.Id));
            Assert.True(ex.ErrorCode == ErrorCode.RecordInInvalidState);
        }

        private T FindContentMessage<T>() where T : AgentMessage
            => _messages.OfType<T>().Single();

        public async Task DisposeAsync()
        {
            if (_issuerWallet != null) await _issuerWallet.AriesStorage.Wallet.CloseAsync();
            if (_holderWallet != null) await _holderWallet.AriesStorage.Wallet.CloseAsync();
            if (_requestorWallet != null) await _requestorWallet.AriesStorage.Wallet.CloseAsync();

            await Wallet.DeleteWalletAsync(IssuerConfig, WalletCredentials);
            await Wallet.DeleteWalletAsync(HolderConfig, WalletCredentials);
            await Wallet.DeleteWalletAsync(RequestorConfig, WalletCredentials);
        }
    }

    [Trait("Category", "DefaultV2")]
    public class ProofTestsV2 : IAsyncLifetime
    {
        static ProofTestsV2()
        {
            global::Hyperledger.Aries.Utils.Runtime.SetFlags(Hyperledger.Aries.Utils.Runtime.LedgerLookupRetryFlag);
        }

        private readonly WalletConfiguration _issuerConfig = TestConstants.TestSingleWalletV2IssuerConfig;
        private readonly WalletConfiguration _holderConfig = TestConstants.TestSingleWalletV2HolderConfig;
        private readonly WalletConfiguration _requestorConfig = TestConstants.TestSingleWalletV2WalletConfig;

        private readonly WalletCredentials _issuerCredentials = TestConstants.TestSingelWalletV2IssuerCreds;
        private readonly WalletCredentials _holderCredentials = TestConstants.TestSingelWalletV2HolderCreds;
        private readonly WalletCredentials _requestorCredentials = TestConstants.TestSingelWalletV2WalletCreds;

        private IAgentContext _issuerWallet;
        private IAgentContext _holderWallet;
        private IAgentContext _requestorWallet;

        private readonly IEventAggregator _eventAggregator;
        private readonly IConnectionService _connectionService;
        private readonly ICredentialService _credentialService;
        private readonly IProofService _proofService;
        private readonly IWalletRecordService _recordService;
        private readonly IPoolService _poolService = new DefaultPoolServiceV2();
        private readonly ISchemaService _schemaService;
        private IWalletService _walletService;

        private readonly ConcurrentBag<AgentMessage> _messages = new ConcurrentBag<AgentMessage>();

        public ProofTestsV2()
        {
            _recordService = new DefaultWalletRecordServiceV2();
            var provisioning = ServiceUtils.GetDefaultMockProvisioningService();

            var ledgerService = new DefaultLedgerServiceV2(
                new DefaultLedgerSigningServiceV2(_recordService),
                _poolService,
                provisioning);

            _eventAggregator = new EventAggregator();

            var messageService = new DefaultMessageService(new Mock<ILogger<DefaultMessageService>>().Object, new IMessageDispatcher[] { });

            var paymentService = new DefaultPaymentService();

            var clientFactory = new Mock<IHttpClientFactory>();
            clientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient());

            _schemaService = new DefaultSchemaServiceV2(provisioning, _recordService, ledgerService, paymentService, Options.Create(new Configuration.AgentOptions()));

            _connectionService = new DefaultConnectionServiceV2(
                _eventAggregator,
                _recordService,
                provisioning,
                new Mock<ILogger<DefaultConnectionServiceV2>>().Object);

            _credentialService = new DefaultCredentialServiceV2(
                _eventAggregator,
                ledgerService,
                _connectionService,
                _recordService,
                _schemaService,
                provisioning,
                paymentService,
                messageService,
                new Mock<ILogger<DefaultCredentialServiceV2>>().Object);

            _proofService = new DefaultProofServiceV2(
                _eventAggregator,
                _connectionService,
                _recordService,
                provisioning,
                ledgerService,
                messageService,
                new Mock<ILogger<DefaultProofServiceV2>>().Object);
        }

        public async Task InitializeAsync()
        {
            _walletService = new DefaultWalletServiceV2();
            _issuerWallet = await AgentUtils.CreateV2(_walletService, _issuerConfig, _issuerCredentials, true);
            _holderWallet = await AgentUtils.CreateV2(_walletService, _holderConfig, _holderCredentials, true);
            _requestorWallet = await AgentUtils.CreateV2(_walletService, _requestorConfig, _requestorCredentials, true);
        }

        [Fact]
        public async Task RequestorInitiatedCredentialProofDemo()
        {
            var events = 0;
            _eventAggregator.GetEventByType<ServiceMessageProcessingEvent>()
                .Where(_ => (_.MessageType == MessageTypes.PresentProofNames.RequestPresentation ||
                             _.MessageType == MessageTypes.PresentProofNames.Presentation))
                .Subscribe(_ =>
                {
                    events++;
                });

            //Setup a connection and issue the credentials to the holder
            var (issuerConnection, holderConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _issuerWallet, _holderWallet);

            await Scenarios.IssueCredentialAsync(
                _recordService, _schemaService, _credentialService, _messages, issuerConnection,
                holderConnection, _issuerWallet, _holderWallet, TestConstants.DefaultMasterSecret, new List<CredentialPreviewAttribute>
                {
                    new CredentialPreviewAttribute("first_name", "Test"),
                    new CredentialPreviewAttribute("last_name", "Holder")
                });

            _messages.Clear();

            //Requestor initialize a connection with the holder
            var (holderRequestorConnection, requestorConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _holderWallet, _requestorWallet);

            await Scenarios.RequestorInitiatedProofProtocolAsync(_proofService, _messages, holderRequestorConnection, requestorConnection,
                _holderWallet, _requestorWallet, new ProofRequest()
                {
                    Name = "ProofReq",
                    Version = "1.0",
                    Nonce = await IndySharedRsPresReq.GenerateNonceAsync(),
                    RequestedAttributes = new Dictionary<string, ProofAttributeInfo>
                    {
                        {"first-name-requirement", new ProofAttributeInfo {Name = "first_name"}}
                    }
                });

            _messages.Clear();

            Assert.True(events == 2);
        }

        [Fact]
        public async Task ProposerInitiatedCredentialProofDemo()
        {
            var events = 0;
            _eventAggregator.GetEventByType<ServiceMessageProcessingEvent>()
                .Where(_ => (_.MessageType == MessageTypes.PresentProofNames.ProposePresentation ||
                             _.MessageType == MessageTypes.PresentProofNames.RequestPresentation ||
                             _.MessageType == MessageTypes.PresentProofNames.Presentation))
                .Subscribe(_ =>
                {
                    events++;
                });

            //Setup a connection and issue the credentials to the holder
            var (issuerConnection, holderConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _issuerWallet, _holderWallet);

            var (issuerCredential, holderCredential) = await Scenarios.IssueCredentialAsync(
                _recordService, _schemaService, _credentialService, _messages, issuerConnection,
                holderConnection, _issuerWallet, _holderWallet, TestConstants.DefaultMasterSecret, new List<CredentialPreviewAttribute>
                {
                    new CredentialPreviewAttribute("first_name", "Test"),
                    new CredentialPreviewAttribute("last_name", "Holder")
                });

            _messages.Clear();

            //Requestor initialize a connection with the holder
            var (holderRequestorConnection, requestorConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _holderWallet, _requestorWallet);

            var (holderProofRecord, requestorProofRecord) = await Scenarios.ProposerInitiatedProofProtocolAsync(_proofService, _messages, holderRequestorConnection, requestorConnection,
                _holderWallet, _requestorWallet, new ProofProposal()
                {
                    Comment = "Hello, World",
                    ProposedAttributes = new List<ProposedAttribute>
                    {
                        new ProposedAttribute
                        {
                            Name = "first_name",
                            CredentialDefinitionId = holderCredential.CredentialDefinitionId,
                            Referent = "0",
                            Value = "Test"
                        }
                    }
                });

            _messages.Clear();

            Assert.True(events == 3);

        }

        [Fact]
        public async Task CreateProofRequestFromProposal()
        {
            var events = 0;
            _eventAggregator.GetEventByType<ServiceMessageProcessingEvent>()
                .Where(_ => (_.MessageType == MessageTypes.PresentProofNames.ProposePresentation ||
                             _.MessageType == MessageTypes.PresentProofNames.RequestPresentation ||
                             _.MessageType == MessageTypes.IssueCredentialNames.RequestCredential ||
                             _.MessageType == MessageTypes.IssueCredentialNames.IssueCredential))
                .Subscribe(_ =>
                {
                    events++;
                });

            // Setup secure connection between issuer and holder
            var (issuerConnection, holderConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _issuerWallet, _holderWallet);

            var (issuerCredential, holderCredential) = await Scenarios.IssueCredentialAsync(
                _recordService, _schemaService, _credentialService, _messages, issuerConnection,
                holderConnection, _issuerWallet, _holderWallet, TestConstants.DefaultMasterSecret, new List<CredentialPreviewAttribute>
                {
                     new CredentialPreviewAttribute("first_name", "Test"),
                     new CredentialPreviewAttribute("last_name", "Test"),
                     new CredentialPreviewAttribute("salary", "100000"),
                     new CredentialPreviewAttribute("age", "25"),
                     new CredentialPreviewAttribute("wellbeing", "100")
                });



            Assert.Equal(issuerCredential.State, holderCredential.State);
            Assert.Equal(CredentialState.Issued, issuerCredential.State);
            var (message, record) = await _proofService.CreateProposalAsync(_holderWallet, new ProofProposal
            {
                Comment = "Hello, World",
                ProposedAttributes = new List<ProposedAttribute>
                 {
                     new ProposedAttribute
                     {
                         Name = "first_name",
                         CredentialDefinitionId = holderCredential.CredentialDefinitionId,
                         Referent = "Proof of Name",
                         Value = "Joe"
                     },
                     new ProposedAttribute
                     {
                         Name = "last_name",
                         CredentialDefinitionId = holderCredential.CredentialDefinitionId,
                         Referent = "Proof of Name",
                         Value = "Shmoe"
                     },
                     new ProposedAttribute
                     {
                         Name = "age",
                         CredentialDefinitionId = holderCredential.CredentialDefinitionId,
                         Referent = "Proof of Age",
                         Value = "Shmoe"
                     }
                },
                ProposedPredicates = new List<ProposedPredicate>
                {
                    new ProposedPredicate
                    {
                        Name = "salary",
                        CredentialDefinitionId = holderCredential.CredentialDefinitionId,
                        Predicate = ">",
                        Threshold = 99999,
                        Referent = "Proof of Salary > $99,999"

                    },
                    new ProposedPredicate
                     {
                         Name = "wellbeing",
                         CredentialDefinitionId = holderCredential.CredentialDefinitionId,
                         Referent = "Proof of Wellbeing",
                         Predicate = "<",
                         Threshold = 99999
                     }
                }
            }, holderConnection.Id);
            Assert.NotNull(message);

            // Process Proposal 
            record = await _proofService.ProcessProposalAsync(_issuerWallet, message, issuerConnection);

            // 
            RequestPresentationMessage requestMessage;

            (requestMessage, record) = await _proofService.CreateRequestFromProposalAsync(_issuerWallet, new ProofRequestParameters
            {
                Name = "Test",
                Version = "1.0",
                NonRevoked = null
            }, record.Id, issuerConnection.Id);

            Assert.NotNull(requestMessage);
            Assert.NotNull(record);

            var actualProofRequest = record.RequestJson.ToObject<ProofRequest>();
            var expectedProofRequest = new ProofRequest
            {
                Name = "Test",
                Version = "1.0",
                Nonce = actualProofRequest.Nonce,
                RequestedAttributes = new Dictionary<string, ProofAttributeInfo>
                {
                    {
                        "Proof of Name",new ProofAttributeInfo
                        {
                            Name=null,
                            Names= new string[] {"first_name", "last_name" },
                            NonRevoked=null,
                            Restrictions=new List<AttributeFilter>
                            {
                                new AttributeFilter
                                {
                                    CredentialDefinitionId = holderCredential.CredentialDefinitionId
                                }
                            }
                        }
                    },
                    {
                        "Proof of Age", new ProofAttributeInfo
                        {
                            Name="age",
                            Names=null,
                            NonRevoked=null,
                            Restrictions=new List<AttributeFilter>
                            {
                                new AttributeFilter
                                {
                                    CredentialDefinitionId = holderCredential.CredentialDefinitionId
                                }
                            }
                        }
                    }
                },
                RequestedPredicates = new Dictionary<string, ProofPredicateInfo>
                {
                    {
                        "Proof of Salary > $99,999", new ProofPredicateInfo
                        {
                            Name = "salary",
                            Names = null,
                            NonRevoked = null,
                            PredicateType = ">",
                            PredicateValue = 99999,
                            Restrictions=new List<AttributeFilter>
                            {
                                new AttributeFilter
                                {
                                    CredentialDefinitionId = holderCredential.CredentialDefinitionId
                                }
                            }
                        }
                    },
                    {
                        "Proof of Wellbeing", new ProofPredicateInfo
                        {
                            Name = "wellbeing",
                            Names = null,
                            NonRevoked = null,
                            PredicateType = "<",
                            PredicateValue = 99999,
                            Restrictions=new List<AttributeFilter>
                            {
                                new AttributeFilter
                                {
                                    CredentialDefinitionId = holderCredential.CredentialDefinitionId
                                }
                            }
                        }
                    }
                }
            };
            var expectedProofRecord = new ProofRecord
            {
                State = ProofState.Requested,
                RequestJson = expectedProofRequest.ToJson(),
            };

            actualProofRequest.Should().BeEquivalentTo(expectedProofRequest);
        }

        [Fact]
        public async Task SendProofProposalThrowsConnectionInvalidState()
        {
            var connectionId = Guid.NewGuid().ToString();

            await _connectionService.CreateInvitationAsync(_issuerWallet,
                new InviteConfiguration { ConnectionId = connectionId });

            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _proofService.CreateProposalAsync(_issuerWallet, new ProofProposal
            {
                Comment = "Hello, World",
                ProposedAttributes = new List<ProposedAttribute>
                {
                    new ProposedAttribute()
                    {
                        Name = "first_name",
                        CredentialDefinitionId = "asdf",
                        Referent = "asdf",
                        Value = "Joe"
                    }
                }
            }, connectionId));

            Assert.True(ex.ErrorCode == ErrorCode.RecordInInvalidState);
        }

        [Fact]
        public async Task CreateProofProposalSuccesfully()
        {
            var proposedAttributes = new List<ProposedAttribute>
            {
                new ProposedAttribute
                {
                    Name = "first_name",
                    CredentialDefinitionId = "1",
                    SchemaId = "1",
                    IssuerDid ="1",
                    Referent = "Proof of Name",
                    Value = "Joe"
                },
                new ProposedAttribute
                {

                    Name = "second_name",
                    CredentialDefinitionId = "1",
                    SchemaId = "1",
                    IssuerDid ="1",
                    Referent = "Proof of Name",
                    Value = "Joe"
                },
                new ProposedAttribute
                {

                    Name = "age",
                    CredentialDefinitionId = "1",
                    SchemaId = "1",
                    IssuerDid ="1",
                    Referent = "Proof of Age",
                    Value = "Joe"
                }
            };
            var proposedPredicates =
            new List<ProposedPredicate>
            {
                new ProposedPredicate
                {
                    Name = "salary",
                    CredentialDefinitionId = "1",
                    Predicate = ">",
                    Threshold = 99999,
                    Referent = "Proof of Salary > $99,999"

                },
                new ProposedPredicate
                {
                    Name = "test",
                    CredentialDefinitionId = "1",
                    Predicate = ">",
                    Threshold = 99999,
                    Referent = "Proof of Test > $99,999"

                }

            };

            var (issuerConnection, holderConnection) = await Scenarios.EstablishConnectionAsync(
               _connectionService, _messages, _issuerWallet, _holderWallet);

            var proofProposal = new ProofProposal
            {
                Comment = "Hello, World",
                ProposedAttributes = proposedAttributes,
                ProposedPredicates = proposedPredicates
            };

            var (message, record) = await _proofService.CreateProposalAsync(_holderWallet, proofProposal, holderConnection.Id);

            var expectedMessage = new ProposePresentationMessage
            {
                Id = message.Id,
                Comment = "Hello, World",
                PresentationPreviewMessage = new PresentationPreviewMessage
                {
                    Id = message.PresentationPreviewMessage.Id,
                    ProposedAttributes = proposedAttributes.ToArray(),
                    ProposedPredicates = proposedPredicates.ToArray()
                }
            };

            message.Should().BeEquivalentTo(expectedMessage);

        }

        [Fact]
        public async Task CreateProofProposalThrowsInvalidParameterFormat()
        {
            // Setup secure connection between issuer and holder
            var (issuerConnection, holderConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _issuerWallet, _holderWallet);

            var proofProposal = new ProofProposal
            {
                Comment = "Hello, World",
                ProposedAttributes = new List<ProposedAttribute>
                 {
                     new ProposedAttribute
                     {
                         Name = "first_name",
                         CredentialDefinitionId = "1",
                         SchemaId = "1",
                         IssuerDid ="1",
                         Referent = "Proof of First Name",
                         Value = "Joe"
                     },
                     new ProposedAttribute
                     {

                         Name = "second_name",
                         CredentialDefinitionId = "2",
                         SchemaId = "2",
                         IssuerDid ="2",
                         Referent = "Proof of First Name",
                         Value = "Joe"
                     }
                 },
                ProposedPredicates = new List<ProposedPredicate>
                {
                    new ProposedPredicate
                    {
                        Name = "salary",
                        CredentialDefinitionId = "1",
                        Predicate = ">",
                        Threshold = 99999,
                        Referent = "Proof of Salary > $99,999"

                    }
                }
            };

            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () =>
                await _proofService.CreateProposalAsync(_holderWallet, proofProposal, holderConnection.Id));

            Assert.True(ex.ErrorCode == ErrorCode.InvalidParameterFormat);

            var len = proofProposal.ProposedAttributes.Count - 1;
            proofProposal.ProposedAttributes.Remove(proofProposal.ProposedAttributes[len]);
            proofProposal.ProposedPredicates.Add(new ProposedPredicate
            {
                Name = "name",
                CredentialDefinitionId = "2",
                Predicate = ">",
                Threshold = 99999,
                Referent = "Proof of Salary > $99,999"

            });

            ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () =>
              await _proofService.CreateProposalAsync(_holderWallet, proofProposal, holderConnection.Id));
            Assert.True(ex.ErrorCode == ErrorCode.InvalidParameterFormat);

        }

        [Fact]
        public async Task ProcessProofRecordNotFound()
        {
            var (issuerConnection, _) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _issuerWallet, _holderWallet);

            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () =>
                await _proofService.ProcessPresentationAsync(_issuerWallet, new PresentationMessage()));

            Assert.True(ex.ErrorCode == ErrorCode.RecordNotFound);
        }

        [Fact]
        public async Task ProcessProofInvalidState()
        {
            //Setup a connection and issue the credentials to the holder
            var (issuerConnection, holderConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _issuerWallet, _holderWallet);

            await Scenarios.IssueCredentialAsync(
                _recordService, _schemaService, _credentialService, _messages, issuerConnection,
                holderConnection, _issuerWallet, _holderWallet, TestConstants.DefaultMasterSecret, new List<CredentialPreviewAttribute>
                {
                    new CredentialPreviewAttribute("first_name", "Test"),
                    new CredentialPreviewAttribute("last_name", "Holder"),
                    new CredentialPreviewAttribute("age", "30")
                });

            _messages.Clear();

            //Requestor initialize a connection with the holder
            var (_, requestorConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _holderWallet, _requestorWallet);

            // Verifier sends a proof request to prover
            {
                var proofRequestObject = new ProofRequest
                {
                    Name = "ProofReq",
                    Version = "1.0",
                    Nonce = await IndySharedRsPresReq.GenerateNonceAsync(),
                    RequestedAttributes = new Dictionary<string, ProofAttributeInfo>
                    {
                        {"first-name-requirement", new ProofAttributeInfo {Name = "first_name"}}
                    },
                    RequestedPredicates = new Dictionary<string, ProofPredicateInfo>
                    {
                        {"age-requirement", new ProofPredicateInfo{Name = "age", Names = null, PredicateType = ">", PredicateValue = 20} }
                    }
                };

                //Requestor sends a proof request
                var (message, _) = await _proofService.CreateRequestAsync(_requestorWallet, proofRequestObject, requestorConnection.Id);
                _messages.Add(message);
            }

            // Holder accepts the proof requests and builds a proof
            {
                // Holder retrieves proof request message from their cloud agent
                var proofRequest = FindContentMessage<RequestPresentationMessage>();
                Assert.NotNull(proofRequest);

                // Holder stores the proof request
                var holderProofRequestId = await _proofService.ProcessRequestAsync(_holderWallet, proofRequest, holderConnection);
                var holderProofRecord = await _proofService.GetAsync(_holderWallet, holderProofRequestId.Id);
                var holderProofObject =
                    JsonConvert.DeserializeObject<ProofRequest>(holderProofRecord.RequestJson);

                var requestedCredentials = new RequestedCredentials();
                foreach (var requestedAttribute in holderProofObject.RequestedAttributes)
                {
                    var credentials =
                        await _proofService.ListCredentialsForProofRequestAsync(_holderWallet, holderProofObject,
                            requestedAttribute.Key);

                    requestedCredentials.RequestedAttributes.Add(requestedAttribute.Key,
                        new RequestedAttribute
                        {
                            CredentialId = credentials.First().CredentialInfo.Referent,
                            Revealed = true
                        });
                }

                foreach (var requestedAttribute in holderProofObject.RequestedPredicates)
                {
                    var credentials =
                        await _proofService.ListCredentialsForProofRequestAsync(_holderWallet, holderProofObject,
                            requestedAttribute.Key);

                    requestedCredentials.RequestedPredicates.Add(requestedAttribute.Key,
                        new RequestedAttribute
                        {
                            CredentialId = credentials.First().CredentialInfo.Referent,
                            Revealed = true
                        });
                }

                //Holder accepts the proof request and sends a proof
                (var proofMessage, var _) = await _proofService.CreatePresentationAsync(_holderWallet, holderProofRequestId.Id,
                    requestedCredentials);
                _messages.Add(proofMessage);
            }

            //Requestor retrives proof message from their cloud agent
            var proof = FindContentMessage<PresentationMessage>();
            Assert.NotNull(proof);

            //Requestor stores proof
            await _proofService.ProcessPresentationAsync(_requestorWallet, proof);

            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _proofService.ProcessPresentationAsync(_requestorWallet, proof));

            Assert.True(ex.ErrorCode == ErrorCode.RecordInInvalidState);
        }

        [Fact]
        public async Task AcceptProofRequestCredentialNotFound()
        {
            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _proofService.CreatePresentationAsync(_issuerWallet, "bad-proof-id", null));
            Assert.True(ex.ErrorCode == ErrorCode.RecordNotFound);
        }

        [Fact]
        public async Task AcceptProofRequestCredentialInvalidState()
        {
            //Setup a connection and issue the credentials to the holder
            var (issuerConnection, holderConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _issuerWallet, _holderWallet);

            _ = await Scenarios.IssueCredentialAsync(
                _recordService, _schemaService, _credentialService, _messages, issuerConnection,
                holderConnection, _issuerWallet, _holderWallet, TestConstants.DefaultMasterSecret, new List<CredentialPreviewAttribute>
                {
                    new CredentialPreviewAttribute("first_name", "Test"),
                    new CredentialPreviewAttribute("last_name", "Holder")
                });

            _messages.Clear();

            _ = await Scenarios.IssueCredentialAsync(
                _recordService, _schemaService, _credentialService, _messages, issuerConnection,
                holderConnection, _issuerWallet, _holderWallet, TestConstants.DefaultMasterSecret, new List<CredentialPreviewAttribute>
                {
                    new CredentialPreviewAttribute("age", "30")
                });

            _messages.Clear();

            //Requestor initialize a connection with the holder
            var (_, requestorConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _holderWallet, _requestorWallet);

            // Verifier sends a proof request to prover
            {
                var proofRequestObject = new ProofRequest
                {
                    Name = "ProofReq",
                    Version = "1.0",
                    Nonce = await IndySharedRsPresReq.GenerateNonceAsync(),
                    RequestedAttributes = new Dictionary<string, ProofAttributeInfo>
                    {
                        {"name-requirement", new ProofAttributeInfo {Names = new string[]{"first_name", "last_name" } } },
                        {"age-requirement", new ProofAttributeInfo {Name = "age"}}
                    }
                };

                //Requestor sends a proof request
                var (message, _) = await _proofService.CreateRequestAsync(_requestorWallet, proofRequestObject, requestorConnection.Id);
                _messages.Add(message);
            }

            // Holder accepts the proof requests and builds a proof
            //Holder retrives proof request message from their cloud agent
            var proofRequest = FindContentMessage<RequestPresentationMessage>();
            Assert.NotNull(proofRequest);

            //Holder stores the proof request
            var holderProofRequestId = await _proofService.ProcessRequestAsync(_holderWallet, proofRequest, holderConnection);
            var holderProofRecord = await _proofService.GetAsync(_holderWallet, holderProofRequestId.Id);
            var holderProofObject =
                JsonConvert.DeserializeObject<ProofRequest>(holderProofRecord.RequestJson);

            var requestedCredentials = new RequestedCredentials();
            foreach (var requestedAttribute in holderProofObject.RequestedAttributes)
            {
                var credentials =
                    await _proofService.ListCredentialsForProofRequestAsync(_holderWallet, holderProofObject,
                        requestedAttribute.Key);

                requestedCredentials.RequestedAttributes.Add(requestedAttribute.Key,
                    new RequestedAttribute
                    {
                        CredentialId = credentials.First().CredentialInfo.Referent,
                        Revealed = true
                    });
            }

            foreach (var requestedAttribute in holderProofObject.RequestedPredicates)
            {
                var credentials =
                    await _proofService.ListCredentialsForProofRequestAsync(_holderWallet, holderProofObject,
                        requestedAttribute.Key);

                requestedCredentials.RequestedPredicates.Add(requestedAttribute.Key,
                    new RequestedAttribute
                    {
                        CredentialId = credentials.First().CredentialInfo.Referent,
                        Revealed = true
                    });
            }

            //Holder accepts the proof request and sends a proof
            await _proofService.CreatePresentationAsync(_holderWallet, holderProofRequestId.Id, requestedCredentials);
            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _proofService.CreatePresentationAsync(_holderWallet, holderProofRequestId.Id,
                requestedCredentials));

            Assert.True(ex.ErrorCode == ErrorCode.RecordInInvalidState);
        }

        [Fact]
        public async Task AcceptProofRequestNoMatchingCredentials()
        {
            //Setup a connection and issue the credentials to the holder
            var (issuerConnection, holderConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _issuerWallet, _holderWallet);

            _ = await Scenarios.IssueCredentialAsync(
                _recordService, _schemaService, _credentialService, _messages, issuerConnection,
                holderConnection, _issuerWallet, _holderWallet, TestConstants.DefaultMasterSecret, new List<CredentialPreviewAttribute>
                {
                    new CredentialPreviewAttribute("first_name", "Test"),
                    new CredentialPreviewAttribute("last_name", "Holder")
                });

            _messages.Clear();

            _ = await Scenarios.IssueCredentialAsync(
                _recordService, _schemaService, _credentialService, _messages, issuerConnection,
                holderConnection, _issuerWallet, _holderWallet, TestConstants.DefaultMasterSecret, new List<CredentialPreviewAttribute>
                {
                    new CredentialPreviewAttribute("age", "30")
                });

            _messages.Clear();

            //Requestor initialize a connection with the holder
            var (_, requestorConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _holderWallet, _requestorWallet);

            // Verifier sends a proof request to prover
            {
                var proofRequestObject = new ProofRequest
                {
                    Name = "ProofReq",
                    Version = "1.0",
                    Nonce = await IndySharedRsPresReq.GenerateNonceAsync(),
                    RequestedAttributes = new Dictionary<string, ProofAttributeInfo>
                    {
                        {
                            "name-requirement", 
                            new ProofAttributeInfo 
                            {
                                Names = new string[]{ "first_name", "last_name" },
                                Restrictions = new List<AttributeFilter>
                                {
                                    new AttributeFilter
                                    {
                                        SchemaId = "testSchemaId",
                                        SchemaIssuerDid = "testSchemaIssuerDid",
                                        SchemaName = "testSchemaName",
                                        CredentialDefinitionId = "testCredentialDefinitionId",
                                        AttributeValue = new AttributeValue { Name = "testName", Value = "testValue"},
                                        IssuerDid = "testIssuerDid",
                                        SchemaVersion = "testSchemaVersion"
                                    }
                                }
                            } 
                        }
                    }
                };

                //Requestor sends a proof request
                var (message, _) = await _proofService.CreateRequestAsync(_requestorWallet, proofRequestObject, requestorConnection.Id);
                _messages.Add(message);
            }

            // Holder accepts the proof requests and builds a proof
            //Holder retrives proof request message from their cloud agent
            var proofRequest = FindContentMessage<RequestPresentationMessage>();
            Assert.NotNull(proofRequest);

            //Holder stores the proof request
            var holderProofRequestId = await _proofService.ProcessRequestAsync(_holderWallet, proofRequest, holderConnection);
            var holderProofRecord = await _proofService.GetAsync(_holderWallet, holderProofRequestId.Id);
            var holderProofObject =
                JsonConvert.DeserializeObject<ProofRequest>(holderProofRecord.RequestJson);

            var requestedCredentials = new RequestedCredentials();
            foreach (var requestedAttribute in holderProofObject.RequestedAttributes)
            {
                var credentials =
                    await _proofService.ListCredentialsForProofRequestAsync(_holderWallet, holderProofObject,
                        requestedAttribute.Key);

                requestedCredentials.RequestedAttributes.Add(requestedAttribute.Key,
                    new RequestedAttribute
                    {
                        CredentialId = credentials.First().CredentialInfo.Referent,
                        Revealed = true
                    });
            }

            foreach (var requestedAttribute in holderProofObject.RequestedPredicates)
            {
                var credentials =
                    await _proofService.ListCredentialsForProofRequestAsync(_holderWallet, holderProofObject,
                        requestedAttribute.Key);

                requestedCredentials.RequestedPredicates.Add(requestedAttribute.Key,
                    new RequestedAttribute
                    {
                        CredentialId = credentials.First().CredentialInfo.Referent,
                        Revealed = true
                    });
            }

            //Holder accepts the proof request and sends a proof
            await _proofService.CreatePresentationAsync(_holderWallet, holderProofRequestId.Id, requestedCredentials);
            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _proofService.CreatePresentationAsync(_holderWallet, holderProofRequestId.Id,
                requestedCredentials));

            Assert.True(ex.ErrorCode == ErrorCode.RecordInInvalidState);
        }

        [Fact]
        public async Task AcceptProofRequestWrongProofRequest()
        {
            //Setup a connection and issue the credentials to the holder
            var (issuerConnection, holderConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _issuerWallet, _holderWallet);

            _ = await Scenarios.IssueCredentialAsync(
                _recordService, _schemaService, _credentialService, _messages, issuerConnection,
                holderConnection, _issuerWallet, _holderWallet, TestConstants.DefaultMasterSecret, new List<CredentialPreviewAttribute>
                {
                    new CredentialPreviewAttribute("first_name", "Test"),
                    new CredentialPreviewAttribute("last_name", "Holder")
                });

            _messages.Clear();

            //Requestor initialize a connection with the holder
            var (_, requestorConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _holderWallet, _requestorWallet);

            // Verifier sends a proof request to prover
            {
                var proofRequestObject = new ProofRequest
                {
                    Name = "ProofReq",
                    Version = "1.0",
                    Nonce = await IndySharedRsPresReq.GenerateNonceAsync(),
                    RequestedAttributes = new Dictionary<string, ProofAttributeInfo>
                    {
                        {
                            "name-requirement",
                            new ProofAttributeInfo
                            {
                            }
                        }
                    }
                };

                //Requestor sends a proof request
                var (message, _) = await _proofService.CreateRequestAsync(_requestorWallet, proofRequestObject, requestorConnection.Id);
                _messages.Add(message);
            }

            // Holder accepts the proof requests and builds a proof
            //Holder retrives proof request message from their cloud agent
            var proofRequest = FindContentMessage<RequestPresentationMessage>();
            Assert.NotNull(proofRequest);

            //Holder stores the proof request
            var holderProofRequestId = await _proofService.ProcessRequestAsync(_holderWallet, proofRequest, holderConnection);
            var holderProofRecord = await _proofService.GetAsync(_holderWallet, holderProofRequestId.Id);
            var holderProofObject =
                JsonConvert.DeserializeObject<ProofRequest>(holderProofRecord.RequestJson);

            var requestedCredentials = new RequestedCredentials();
            foreach (var requestedAttribute in holderProofObject.RequestedAttributes)
            {
                var credentials =
                    await _proofService.ListCredentialsForProofRequestAsync(_holderWallet, holderProofObject,
                        requestedAttribute.Key);

                requestedCredentials.RequestedAttributes.Add(requestedAttribute.Key,
                    new RequestedAttribute
                    {
                        CredentialId = credentials.First().CredentialInfo.Referent,
                        Revealed = true
                    });
            }

            foreach (var requestedAttribute in holderProofObject.RequestedPredicates)
            {
                var credentials =
                    await _proofService.ListCredentialsForProofRequestAsync(_holderWallet, holderProofObject,
                        requestedAttribute.Key);

                requestedCredentials.RequestedPredicates.Add(requestedAttribute.Key,
                    new RequestedAttribute
                    {
                        CredentialId = credentials.First().CredentialInfo.Referent,
                        Revealed = true
                    });
            }

            //Holder accepts the proof request and sends a proof
            await _proofService.CreatePresentationAsync(_holderWallet, holderProofRequestId.Id, requestedCredentials);
            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _proofService.CreatePresentationAsync(_holderWallet, holderProofRequestId.Id,
                requestedCredentials));

            Assert.True(ex.ErrorCode == ErrorCode.RecordInInvalidState);
        }

        [Fact]
        public async Task RejectProofRequestCredentialNotFound()
        {
            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _proofService.RejectProofRequestAsync(_issuerWallet, "bad-proof-id"));
            Assert.True(ex.ErrorCode == ErrorCode.RecordNotFound);
        }

        [Fact]
        public async Task RejectProofRequestCredentialInvalidState()
        {
            //Setup a connection and issue the credentials to the holder
            var (issuerConnection, holderConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _issuerWallet, _holderWallet);

            await Scenarios.IssueCredentialAsync(
                _recordService, _schemaService, _credentialService, _messages, issuerConnection,
                holderConnection, _issuerWallet, _holderWallet, TestConstants.DefaultMasterSecret, new List<CredentialPreviewAttribute>
                {
                    new CredentialPreviewAttribute("first_name", "Test"),
                    new CredentialPreviewAttribute("last_name", "Holder")
                });

            _messages.Clear();

            //Requestor initialize a connection with the holder
            var (_, requestorConnection) = await Scenarios.EstablishConnectionAsync(
                _connectionService, _messages, _holderWallet, _requestorWallet);

            // Verifier sends a proof request to prover
            {
                var proofRequestObject = new ProofRequest
                {
                    Name = "ProofReq",
                    Version = "1.0",
                    Nonce = await IndySharedRsPresReq.GenerateNonceAsync(),
                    RequestedAttributes = new Dictionary<string, ProofAttributeInfo>
                    {
                        {
                            "first-name-requirement", 
                            new ProofAttributeInfo 
                            {
                                Name = "first_name",
                                Restrictions = new List<AttributeFilter>
                                {
                                    new AttributeFilter{ IssuerDid = "NcYxiDXkpYi6ov5FcYDi1e" },
                                    new AttributeFilter{ IssuerDid = "Th7MpTaRZVRYnPiabds81Y" }
                                }
                            }
                        }
                    }
                };

                //Requestor sends a proof request
                var (message, _) = await _proofService.CreateRequestAsync(_requestorWallet, proofRequestObject, requestorConnection.Id);
                _messages.Add(message);
            }

            //Holder retrieves proof request message from their cloud agent
            var proofRequest = FindContentMessage<RequestPresentationMessage>();
            Assert.NotNull(proofRequest);

            //Holder stores the proof request
            var holderProofRequestId = await _proofService.ProcessRequestAsync(_holderWallet, proofRequest, holderConnection);

            //Holder accepts the proof request and sends a proof
            await _proofService.RejectProofRequestAsync(_holderWallet, holderProofRequestId.Id);

            var ex = await Assert.ThrowsAsync<AriesFrameworkException>(async () => await _proofService.RejectProofRequestAsync(_holderWallet, holderProofRequestId.Id));
            Assert.True(ex.ErrorCode == ErrorCode.RecordInInvalidState);
        }

        private T FindContentMessage<T>() where T : AgentMessage
            => _messages.OfType<T>().Single();

        public async Task DisposeAsync()
        {
            if (_issuerWallet != null)
            {
                await _walletService.DeleteWalletAsync(_issuerConfig, _issuerCredentials);
            }
            if (_holderWallet != null)
            {
                await _walletService.DeleteWalletAsync(_holderConfig, _holderCredentials);
            }
            if (_requestorWallet != null)
            {
                await _walletService.DeleteWalletAsync(_requestorConfig, _requestorCredentials);
            }
        }
    }
}
