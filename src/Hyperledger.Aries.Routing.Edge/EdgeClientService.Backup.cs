﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Decorators.Attachments;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.IssueCredential;
using Hyperledger.Aries.Utils;
using Hyperledger.Indy;
using Hyperledger.Indy.CryptoApi;
using Hyperledger.Indy.DidApi;
using Hyperledger.Indy.WalletApi;
using Multiformats.Base;
using Newtonsoft.Json;

namespace Hyperledger.Aries.Routing.Edge
{
    /// <inheritdoc />
    public partial class EdgeClientService : IEdgeClientService
    {
        const string InternalBackupDid = "22222222AriesBackupDid";

        /// <inheritdoc />
        public async Task<string> CreateBackupAsync(IAgentContext agentContext, string seed)
        {
            if (agentContext.AriesStorage.Wallet is null)
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Indy.WalletApi.Wallet)} which must not be null.");
            }

            if (seed.Length != 32)
            {
                throw new ArgumentException($"{nameof(seed)} should be 32 characters");
            }

            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var json = new { path, key = seed }.ToJson();

            await agentContext.AriesStorage.Wallet.ExportAsync(json);

            var bytesArray = await Task.Run(() => File.ReadAllBytes(path));

            var backupVerkey = await EnsureBackupKeyAsync(agentContext, seed);
            var signedBytesArray = await Crypto.SignAsync(agentContext.AriesStorage.Wallet,  backupVerkey, bytesArray);

            var payload = bytesArray.ToBase64String();

            var backupMessage = new StoreBackupAgentMessage
            {
                BackupId = backupVerkey,
                PayloadSignature = signedBytesArray.ToBase64String(),
                Payload = new List<Attachment>()
                {
                    new Attachment
                    {
                        Id = "libindy-backup-request-0",
                        MimeType = CredentialMimeTypes.ApplicationJsonMimeType,
                        Data = new AttachmentContent
                        {
                            Base64 = payload
                        }
                    }
                }
            };

            var connection = await GetMediatorConnectionAsync(agentContext).ConfigureAwait(false);

            if (connection == null)
                throw new AriesFrameworkException(ErrorCode.RecordNotFound,
                    "Couldn't locate a connection to mediator agent");

            File.Delete(path);

            await _messageService
                .SendReceiveAsync<StoreBackupResponseAgentMessage>(agentContext, backupMessage, connection)
                .ConfigureAwait(false);
            return backupVerkey;
        }

        private static async Task<string> EnsureBackupKeyAsync(IAgentContext agentContext, string seed)
        {
            if (agentContext.AriesStorage.Wallet is null)
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Indy.WalletApi.Wallet)} which must not be null.");
            }

            try
            {
                var didResult = await Did.CreateAndStoreMyDidAsync(agentContext.AriesStorage.Wallet, new
                {
                    did = InternalBackupDid,
                    seed = seed
                }.ToJson());
                return didResult.VerKey;
            }
            catch (IndyException ex) when (ex.SdkErrorCode == 600)
            {
                var key = await Did.ReplaceKeysStartAsync(
                    agentContext.AriesStorage.Wallet,
                    InternalBackupDid,
                    new { seed = seed }.ToJson());

                await Did.ReplaceKeysApplyAsync(agentContext.AriesStorage.Wallet, InternalBackupDid);
                return key;
            }
        }

        /// <inheritdoc />
        public async Task<List<Attachment>> RetrieveBackupAsync(IAgentContext context, string seed, long offset = default)
        {
            var publicKey = await EnsureBackupKeyAsync(context, seed);

            var decodedKey = Multibase.Base58.Decode(publicKey);
            var publicKeySigned = await Crypto.SignAsync(context.AriesStorage.Wallet, publicKey, decodedKey);

            var retrieveBackupResponseMessage = new RetrieveBackupAgentMessage()
            {
                BackupId = publicKey,
                Signature = publicKeySigned.ToBase64String()
            };

            var connection = await GetMediatorConnectionAsync(context).ConfigureAwait(false);
            if (connection == null)
                throw new AriesFrameworkException(ErrorCode.RecordNotFound, "Couldn't locate a connection to mediator agent");

            var response = await _messageService.SendReceiveAsync<RetrieveBackupResponseAgentMessage>(context, retrieveBackupResponseMessage, connection).ConfigureAwait(false);
            return response.Payload;
        }

        /// <inheritdoc />
        public async Task<AgentOptions> RestoreFromBackupAsync(IAgentContext sourceContext,
            string seed,
            List<Attachment> backupData)
        {
            var tempWalletPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var walletBase64 = backupData.First().Data.Base64;
            var walletToRestoreInBytes = walletBase64.GetBytesFromBase64();

            await Task.Run(() => File.WriteAllBytes(tempWalletPath, walletToRestoreInBytes));

            var oldAgentOptionsString = JsonConvert.SerializeObject(_agentOptions);

            var json = new { path = tempWalletPath, key = seed }.ToJson();

            _agentOptions.WalletConfiguration.Id = Guid.NewGuid().ToString();
            _agentOptions.WalletCredentials.Key = Utils.GenerateRandomAsync(32);

            await Wallet.ImportAsync(_agentOptions.WalletConfiguration.ToJson(), _agentOptions.WalletCredentials.ToJson(), json);

            // Try delete the old wallet
            try
            {
                var oldAgentOptions = JsonConvert.DeserializeObject<AgentOptions>(oldAgentOptionsString);
                await _walletService.DeleteWalletAsync(oldAgentOptions.WalletConfiguration,
                    oldAgentOptions.WalletCredentials);
                // Add 1 sec delay to allow filesystem to catch up
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            catch (Exception)
            {
                Console.WriteLine("Wallet could not be deleted");
            }

            File.Delete(tempWalletPath);

            return _agentOptions;
        }

        /// <inheritdoc />
        public async Task<List<long>> ListBackupsAsync(IAgentContext agentContext)
        {
            if (agentContext.AriesStorage.Wallet is null)
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Indy.WalletApi.Wallet)} which must not be null.");
            }

            var publicKey = await Did.KeyForLocalDidAsync(agentContext.AriesStorage.Wallet, InternalBackupDid);

            var listBackupsMessage = new ListBackupsAgentMessage()
            {
                BackupId = publicKey,
            };

            var connection = await GetMediatorConnectionAsync(agentContext).ConfigureAwait(false);
            if (connection == null)
                throw new AriesFrameworkException(ErrorCode.RecordNotFound, "Couldn't locate a connection to mediator agent");

            var response = await _messageService.SendReceiveAsync<ListBackupsResponseAgentMessage>(agentContext, listBackupsMessage, connection).ConfigureAwait(false);
            return response.BackupList.ToList();
        }

        /// <inheritdoc />
        public async Task<AgentOptions> RestoreFromBackupAsync(IAgentContext context, string seed)
        {
            var backupAttachments = await RetrieveBackupAsync(context, seed);
            return await RestoreFromBackupAsync(context, seed, backupAttachments);
        }
    }
}
