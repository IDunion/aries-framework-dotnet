using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Decorators.Attachments;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Routing.Mediator.Storage;
using Hyperledger.Aries.Utils;
using Multiformats.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Routing.Mediator.Handlers
{
    public class RetrieveBackupHandler : IMessageHandler
    {
        private readonly IStorageService _storageService;
        private readonly IEventAggregator _eventAggregator;

        public IEnumerable<MessageType> SupportedMessageTypes => new MessageType[]
        {
            BackupTypeNames.RetrieveBackupAgentMessage,
            BackupTypeNames.RetrieveBackupResponseAgentMessage,
            BackupTypeNames.ListBackupsAgentMessage
        };

        public RetrieveBackupHandler(IStorageService storageService, IEventAggregator eventAggregator)
        {
            _storageService = storageService;
            _eventAggregator = eventAggregator;
        }

        public async Task<AgentMessage> ProcessAsync(IAgentContext agentContext, UnpackedMessageContext messageContext)
        {
            string msgJson = messageContext.GetMessageJson();

            switch (messageContext.GetMessageType())
            {
                case BackupTypeNames.RetrieveBackupAgentMessage:
                    {
                        RetrieveBackupAgentMessage message = messageContext.GetMessage<RetrieveBackupAgentMessage>();

                        byte[] signature = message.Signature.GetBytesFromBase64();
                        byte[] backupId = Multibase.Base58.Decode(message.BackupId);

                        bool result = await CryptoUtils.VerifyAsync(
                            agentContext.AriesStorage,
                            message.BackupId,
                            backupId,
                            signature);


                        if (!result)
                        {
                            throw new ArgumentException($"{nameof(result)} signature does not match the signer");
                        }

                        List<Attachment> backupAttachments = await _storageService.RetrieveBackupAsync(message.BackupId);
                        return new RetrieveBackupResponseAgentMessage
                        {
                            Payload = backupAttachments
                        };
                    }
                case BackupTypeNames.ListBackupsAgentMessage:
                    {
                        ListBackupsAgentMessage message = messageContext.GetMessage<ListBackupsAgentMessage>();
                        IEnumerable<string> backupList = await _storageService.ListBackupsAsync(message.BackupId);
                        IEnumerable<string> timestampList = backupList.Select(p => new DirectoryInfo(p).Name);

                        return new ListBackupsResponseAgentMessage
                        {
                            BackupList = timestampList
                                .Select(x => long.Parse(x))
                                .OrderByDescending(x => x)
                                .ToList()
                        };
                    }
            }

            return null;
        }
    }
}
