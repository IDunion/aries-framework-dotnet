using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.Handshakes.Connection;
using Hyperledger.Aries.Features.Handshakes.Connection.Models;
using Hyperledger.Aries.Storage;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Routing.Mediator
{
    internal class MediatorProvisioningServiceV2 : IHostedService
    {
        internal const string EdgeInvitationTagName = "EdgeInvitationId";
        internal const string InvitationTagName = "Invitation";

        private readonly IConnectionService _connectionService;
        private readonly IProvisioningService _provisioningService;
        private readonly IWalletRecordService _recordService;
        private readonly IAgentProvider _agentProvider;

        public MediatorProvisioningServiceV2(
            IConnectionService connectionService,
            IProvisioningService provisioningService,
            IWalletRecordService recordService,
            IAgentProvider agentProvider)
        {
            _connectionService = connectionService;
            _provisioningService = provisioningService;
            _recordService = recordService;
            _agentProvider = agentProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _provisioningService.ProvisionAgentAsync();
            }
            catch (Exception)
            {
                // OK
            }

            IAgentContext agentContext = await _agentProvider.GetContextAsync();
            ProvisioningRecord provsioningRecord = await _provisioningService.GetProvisioningAsync(agentContext.AriesStorage);

            if (provsioningRecord.GetTag(EdgeInvitationTagName) != null)
            {
                return;
            }

            (ConnectionInvitationMessage invitation, Features.Handshakes.Common.ConnectionRecord record) = await _connectionService.CreateInvitationAsync(
                agentContext: agentContext,
                config: new InviteConfiguration { MultiPartyInvitation = true, AutoAcceptConnection = true });

            invitation.RoutingKeys = null;

            record.SetTag(InvitationTagName, invitation.ToJson());
            provsioningRecord.SetTag(EdgeInvitationTagName, record.Id);
            await _recordService.UpdateAsync(agentContext.AriesStorage, provsioningRecord);
            await _recordService.UpdateAsync(agentContext.AriesStorage, record);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
