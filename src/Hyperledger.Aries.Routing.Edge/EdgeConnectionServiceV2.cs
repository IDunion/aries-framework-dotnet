using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Features.Handshakes.Common;
using Hyperledger.Aries.Features.Handshakes.Connection;
using Hyperledger.Aries.Features.Handshakes.Connection.Models;
using Hyperledger.Aries.Storage;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Routing.Edge
{
    internal class EdgeConnectionServiceV2 : DefaultConnectionServiceV2
    {
        private readonly IEdgeClientService edgeClientService;

        public EdgeConnectionServiceV2(
            IEdgeClientService edgeClientService,
            IEventAggregator eventAggregator,
            IWalletRecordService recordService,
            IProvisioningService provisioningService,
            ILogger<DefaultConnectionServiceV2> logger)
            : base(eventAggregator, recordService, provisioningService, logger)
        {
            this.edgeClientService = edgeClientService;
        }

        /// <inheritdoc />
        public override async Task<(ConnectionRequestMessage, ConnectionRecord)> CreateRequestAsync(IAgentContext agentContext, ConnectionInvitationMessage invitation)
        {
            var (message, record) = await base.CreateRequestAsync(agentContext, invitation);

            await edgeClientService.AddRouteAsync(agentContext, record.MyVk);

            return (message, record);
        }

        /// <inheritdoc />
        public override async Task<(ConnectionResponseMessage, ConnectionRecord)> CreateResponseAsync(IAgentContext agentContext, string connectionId)
        {
            var (message, record) = await base.CreateResponseAsync(agentContext, connectionId);

            await edgeClientService.AddRouteAsync(agentContext, record.MyVk);

            return (message, record);
        }

        /// <inheritdoc />
        public override async Task<(ConnectionInvitationMessage, ConnectionRecord)> CreateInvitationAsync(IAgentContext agentContext, InviteConfiguration config = null)
        {
            var (message, record) = await base.CreateInvitationAsync(agentContext, config);

            await edgeClientService.AddRouteAsync(agentContext, message.RecipientKeys.First());

            return (message, record);
        }
    }
}
