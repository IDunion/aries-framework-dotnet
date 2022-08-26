using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Agents.Edge;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Features.Handshakes.Connection;
using Hyperledger.Aries.Features.Handshakes.Connection.Models;
using Hyperledger.Aries.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Hyperledger.Aries.Tests")]

namespace Hyperledger.Aries.Routing.Edge
{
    internal class EdgeProvisioningServiceV2 : IHostedService, IEdgeProvisioningService
    {
        internal const string MediatorConnectionIdTagName = "MediatorConnectionId";

        private readonly IProvisioningService _provisioningService;
        private readonly IConnectionService _connectionService;
        private readonly IMessageService _messageService;
        private readonly IEdgeClientService _edgeClientService;
        private readonly IWalletRecordService _recordService;
        private readonly IAgentProvider _agentProvider;
        private readonly AgentOptions _options;

        public EdgeProvisioningServiceV2(
            IProvisioningService provisioningService,
            IConnectionService connectionService,
            IMessageService messageService,
            IEdgeClientService edgeClientService,
            IWalletRecordService recordService,
            IAgentProvider agentProvider,
            IOptions<AgentOptions> options)
        {
            _provisioningService = provisioningService;
            _connectionService = connectionService;
            _messageService = messageService;
            _edgeClientService = edgeClientService;
            _recordService = recordService;
            _agentProvider = agentProvider;
            _options = options.Value;
        }

        public async Task ProvisionAsync(AgentOptions options, CancellationToken cancellationToken = default)
        {
            var discovery = await _edgeClientService.DiscoverConfigurationAsync(options.EndpointUri);

            try
            {
                options.AgentKey = discovery.RoutingKey;
                options.EndpointUri = discovery.ServiceEndpoint;

                await _provisioningService.ProvisionAgentAsync(options);
            }
            catch (Exception)
            {
                // OK
            }

            var agentContext = await _agentProvider.GetContextAsync();
            var provisioning = await _provisioningService.GetProvisioningAsync(agentContext.AriesStorage);

            // Check if connection has been established with mediator agent
            if (provisioning.GetTag(MediatorConnectionIdTagName) == null)
            {
                var (request, record) = await _connectionService.CreateRequestAsync(agentContext, discovery.Invitation);
                var response = await _messageService.SendReceiveAsync<ConnectionResponseMessage>(agentContext, request, record);

                await _connectionService.ProcessResponseAsync(agentContext, response, record);

                // Remove the routing key explicitly as it won't ever be needed.
                // Messages will always be sent directly with return routing enabled
                record = await _connectionService.GetAsync(agentContext, record.Id);
                record.Endpoint = new AgentEndpoint(record.Endpoint.Uri, null, null);
                await _recordService.UpdateAsync(agentContext.AriesStorage, record);

                provisioning.SetTag(MediatorConnectionIdTagName, record.Id);
                await _recordService.UpdateAsync(agentContext.AriesStorage, provisioning);
            }

            await _edgeClientService.CreateInboxAsync(agentContext, options.MetaData);
        }

        public Task ProvisionAsync(CancellationToken cancellationToken = default) => ProvisionAsync(_options, cancellationToken);

        public Task StartAsync(CancellationToken cancellationToken) => ProvisionAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    }
}
