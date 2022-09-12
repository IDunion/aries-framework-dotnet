using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.Handshakes.Common;
using Hyperledger.Aries.Storage;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Routing.Edge
{
    public partial class EdgeClientService : IEdgeClientService
    {
        private const string MediatorInboxIdTagName = "MediatorInboxId";
        private const string MediatorInboxKeyTagName = "MediatorInboxKey";
        private const string MediatorConnectionIdTagName = "MediatorConnectionId";
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IProvisioningService _provisioningService;
        private readonly IWalletRecordService _recordService;
        private readonly IWalletService _walletService;
        private readonly IMessageService _messageService;

        private readonly AgentOptions _agentOptions;

        public EdgeClientService(
            IHttpClientFactory httpClientFactory,
            IProvisioningService provisioningService,
            IWalletRecordService recordService,
            IMessageService messageService,
            IWalletService walletService,
            IOptions<AgentOptions> agentOptions)
        {
            _httpClientFactory = httpClientFactory;
            _provisioningService = provisioningService;
            _recordService = recordService;
            _walletService = walletService;
            _messageService = messageService;
            _agentOptions = agentOptions.Value;
        }

        public virtual async Task AddRouteAsync(IAgentContext agentContext, string routeDestination)
        {
            ConnectionRecord connection = await GetMediatorConnectionAsync(agentContext);
            if (connection != null)
            {
                AddRouteMessage createInboxMessage = new() { RouteDestination = routeDestination };
                await _messageService.SendAsync(agentContext, createInboxMessage, connection);
            }
        }

        public virtual async Task CreateInboxAsync(IAgentContext agentContext, Dictionary<string, string> metadata = null)
        {
            ProvisioningRecord provisioning = await _provisioningService.GetProvisioningAsync(agentContext.AriesStorage);
            if (provisioning.GetTag(MediatorInboxIdTagName) != null)
            {
                return;
            }
            ConnectionRecord connection = await GetMediatorConnectionAsync(agentContext);

            CreateInboxMessage createInboxMessage = new() { Metadata = metadata };
            CreateInboxResponseMessage response = await _messageService.SendReceiveAsync<CreateInboxResponseMessage>(agentContext, createInboxMessage, connection);

            provisioning.SetTag(MediatorInboxIdTagName, response.InboxId);
            provisioning.SetTag(MediatorInboxKeyTagName, response.InboxKey);
            await _recordService.UpdateAsync(agentContext.AriesStorage, provisioning);
        }

        internal async Task<ConnectionRecord> GetMediatorConnectionAsync(IAgentContext agentContext)
        {
            ProvisioningRecord provisioning = await _provisioningService.GetProvisioningAsync(agentContext.AriesStorage);
            if (provisioning.GetTag(MediatorConnectionIdTagName) == null)
            {
                return null;
            }
            ConnectionRecord connection = await _recordService.GetAsync<ConnectionRecord>(agentContext.AriesStorage, provisioning.GetTag(MediatorConnectionIdTagName));
            if (connection == null)
            {
                throw new AriesFrameworkException(ErrorCode.RecordNotFound, "Couldn't locate a connection to mediator agent");
            }

            return connection.State != ConnectionState.Connected
                ? throw new AriesFrameworkException(ErrorCode.RecordInInvalidState, $"You must be connected to the mediator agent. Current state is {connection.State}")
                : connection;
        }

        public virtual async Task<AgentPublicConfiguration> DiscoverConfigurationAsync(string agentEndpoint)
        {
            HttpClient httpClient = _httpClientFactory.CreateClient();
            HttpResponseMessage response = await httpClient.GetAsync($"{agentEndpoint}/.well-known/agent-configuration").ConfigureAwait(false);
            string responseJson = await response.Content.ReadAsStringAsync();

            return responseJson.ToObject<AgentPublicConfiguration>();
        }

        public virtual async Task<(int, IEnumerable<InboxItemMessage>)> FetchInboxAsync(IAgentContext agentContext)
        {
            ConnectionRecord connection = await GetMediatorConnectionAsync(agentContext);
            if (connection == null)
            {
                throw new InvalidOperationException("This agent is not configured with a mediator");
            }

            GetInboxItemsMessage createInboxMessage = new();
            GetInboxItemsResponseMessage response = await _messageService.SendReceiveAsync<GetInboxItemsResponseMessage>(agentContext, createInboxMessage, connection);

            List<string> processedItems = new();
            List<InboxItemMessage> unprocessedItem = new();
            foreach (InboxItemMessage item in response.Items)
            {
                try
                {
                    _ = await agentContext.Agent.ProcessAsync(agentContext, new PackedMessageContext(item.Data));
                    processedItems.Add(item.Id);
                }
                catch (AriesFrameworkException e) when (e.ErrorCode == ErrorCode.InvalidMessage)
                {
                    processedItems.Add(item.Id);
                }
                catch (Exception)
                {
                    unprocessedItem.Add(item);
                }
            }

            if (processedItems.Any())
            {
                await _messageService.SendAsync(agentContext, new DeleteInboxItemsMessage { InboxItemIds = processedItems }, connection);
            }

            return (processedItems.Count, unprocessedItem);
        }

        public virtual async Task AddDeviceAsync(IAgentContext agentContext, AddDeviceInfoMessage message)
        {
            ConnectionRecord connection = await GetMediatorConnectionAsync(agentContext);
            if (connection != null)
            {
                await _messageService.SendAsync(agentContext, message, connection);
            }
        }
    }
}
