﻿using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.PresentProof;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Aries.Storage.Records;
using Hyperledger.Aries.Storage.Records.Search;
using Hyperledger.Indy.NonSecretsApi;
using Hyperledger.Indy.WalletApi;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Storage
{
    /// <inheritdoc />
    public class DefaultWalletRecordService : IWalletRecordService
    {
        private readonly JsonSerializerSettings _jsonSettings;

        /// <summary>Initializes a new instance of the <see cref="DefaultWalletRecordService"/> class.</summary>
        public DefaultWalletRecordService()
        {
            _jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Converters = new List<JsonConverter>
                {
                    new AgentEndpointJsonConverter(),
                    new AttributeFilterConverter()
                }
            };
        }

        /// <inheritdoc />
        public virtual Task AddAsync<T>(AriesStorage storage, T record)
            where T : RecordBase, new()
        {
            Debug.WriteLine($"Adding record of type {record.TypeName} with Id {record.Id}");

            record.CreatedAtUtc = DateTime.UtcNow;

            return NonSecrets.AddRecordAsync(storage.Wallet,
                record.TypeName,
                record.Id,
                record.ToJson(_jsonSettings),
                record.Tags.ToJson());
        }

        /// <inheritdoc />
        public virtual async Task<List<T>> SearchAsync<T>(AriesStorage storage, ISearchQuery query, SearchOptions options, int count, int skip)
            where T : RecordBase, new()
        {
            using WalletSearch search = await NonSecrets.OpenSearchAsync(storage.Wallet, new T().TypeName,
                (query ?? SearchQuery.Empty).ToJson(),
                (options ?? new SearchOptions()).ToJson());
            if (skip > 0)
            {
                _ = await search.NextAsync(storage.Wallet, skip);
            }
            SearchResult result = JsonConvert.DeserializeObject<SearchResult>(await search.NextAsync(storage.Wallet, count), _jsonSettings);

            return result.Records?
                       .Select(x =>
                       {
                           T record = JsonConvert.DeserializeObject<T>(x.Value, _jsonSettings);
                           foreach (KeyValuePair<string, string> tag in x.Tags)
                           {
                               record.Tags[tag.Key] = tag.Value;
                           }

                           return record;
                       })
                       .ToList()
                   ?? new List<T>();
        }

        /// <inheritdoc />
        public virtual async Task UpdateAsync(AriesStorage storage, RecordBase record)
        {
            record.UpdatedAtUtc = DateTime.UtcNow;

            await NonSecrets.UpdateRecordValueAsync(storage.Wallet,
                record.TypeName,
                record.Id,
                record.ToJson(_jsonSettings));

            await NonSecrets.UpdateRecordTagsAsync(storage.Wallet,
                record.TypeName,
                record.Id,
                record.Tags.ToJson(_jsonSettings));
        }

        /// <inheritdoc />
        public virtual async Task<T> GetAsync<T>(AriesStorage storage, string id) where T : RecordBase, new()
        {
            try
            {
                string recordJson = await NonSecrets.GetRecordAsync(storage.Wallet,
                    new T().TypeName,
                    id,
                    new SearchOptions().ToJson());

                if (recordJson == null)
                {
                    return null;
                }

                SearchItem item = JsonConvert.DeserializeObject<SearchItem>(recordJson, _jsonSettings);

                T record = JsonConvert.DeserializeObject<T>(item.Value, _jsonSettings);

                foreach (KeyValuePair<string, string> tag in item.Tags)
                {
                    record.Tags[tag.Key] = tag.Value;
                }

                return record;
            }
            catch (WalletItemNotFoundException)
            {
                return null;
            }
        }

        /// <inheritdoc />
        public virtual async Task<bool> DeleteAsync<T>(AriesStorage storage, string id) where T : RecordBase, new()
        {
            try
            {
                T record = await GetAsync<T>(storage, id);
                string typeName = new T().TypeName;

                await NonSecrets.DeleteRecordTagsAsync(
                    wallet: storage.Wallet,
                    type: typeName,
                    id: id,
                    tagsJson: record.Tags.Select(x => x.Key).ToArray().ToJson());
                await NonSecrets.DeleteRecordAsync(
                    wallet: storage.Wallet,
                    type: typeName,
                    id: id);

                return true;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Couldn't delete record: {e}");
                return false;
            }
        }

        public Task AddKeyAsync(AriesStorage storage, IntPtr keyHandle, string verkey)
        {
            throw new NotImplementedException();
        }

        public Task<IntPtr> GetKeyAsync(AriesStorage storage, string verkey)
        {
            throw new NotImplementedException();
        }
    }
}
