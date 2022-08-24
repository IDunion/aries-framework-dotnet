using aries_askar_dotnet;
using aries_askar_dotnet.Models;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.PresentProof;
using Hyperledger.Aries.Storage.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AriesAskarResults = aries_askar_dotnet.AriesAskar.ResultListApi;
using AriesAskarStore = aries_askar_dotnet.AriesAskar.StoreApi;

namespace Hyperledger.Aries.Storage
{
    /// <inheritdoc />
    public class DefaultWalletRecordServiceV2 : IWalletRecordService
    {
        private readonly JsonSerializerSettings _jsonSettings;

        /// <summary>Initializes a new instance of the <see cref="DefaultWalletRecordService"/> class.</summary>
        public DefaultWalletRecordServiceV2()
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
        public virtual async Task AddAsync<T>(AriesStorage storage, T record)
            where T : RecordBase, new()
        {
            if (storage.Store is null)
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Store)} which must not be null.");
            }

            Debug.WriteLine($"Adding record of type {record.TypeName} with Id {record.Id}");

            record.CreatedAtUtc = DateTime.UtcNow;

            if (storage.Store.session == null)
            {
                _ = await AriesAskarStore.StartSessionAsync(storage.Store);
            }

            _ = await AriesAskarStore.InsertAsync(
                session: storage.Store.session,
                category: record.TypeName,
                name: record.Id,
                value: record.ToJson(_jsonSettings),
                tags: record.Tags.ToJson());
        }

        /// <inheritdoc />
        // TODO : ??? - change SearchAsync to use the aries-askar methods
        public virtual async Task<List<T>> SearchAsync<T>(AriesStorage storage, ISearchQuery query, SearchOptions options, int count, int skip)
            where T : RecordBase, new()
        {
            if (storage.Store is null)
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Store)} which must not be null.");
            }

            if (storage.Store.session == null)
            {
                _ = await AriesAskarStore.StartSessionAsync(storage.Store);
            }

            options ??= new SearchOptions();

            Scan search = await AriesAskarStore.StartScanAsync(
                store: storage.Store,
                category: new T().TypeName,
                tagFilter: (query ?? SearchQuery.Empty).ToJson(),
                offset: skip,
                limit: count);
            IntPtr entryListHandle = await AriesAskarStore.NextAsync(search);

            //empty result for given filter 
            if (entryListHandle == new IntPtr())
            {
                return new List<T>();
            }

            List<SearchItem> records = new();
            int numRecords = await AriesAskarResults.EntryListCountAsync(entryListHandle);
            for (int i = 0; i < numRecords; i++)
            {
                string tagsJson = options.RetrieveTags ? await AriesAskarResults.EntryListGetTagsAsync(entryListHandle, i) : null;

                SearchItem item = new()
                {
                    Id = await AriesAskarResults.EntryListGetNameAsync(entryListHandle, i),
                    Type = options.RetrieveType ? await AriesAskarResults.EntryListGetCategoryAsync(entryListHandle, i) : null,
                    Value = options.RetrieveValue ? await AriesAskarResults.EntryListGetValueAsync(entryListHandle, i) : null,
                    Tags = !string.IsNullOrEmpty(tagsJson) ? JsonConvert.DeserializeObject<Dictionary<string, string>>(tagsJson) : null,
                };
                records.Add(item);
            }
            SearchResult result = new() { Records = records };

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
            if (storage.Store is null)
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Store)} which must not be null.");
            }

            record.UpdatedAtUtc = DateTime.UtcNow;
            if (storage.Store.session == null)
            {
                _ = await AriesAskarStore.StartSessionAsync(storage.Store);
            }

            _ = await AriesAskarStore.ReplaceAsync(
                session: storage.Store.session,
                category: record.TypeName,
                name: record.Id,
                value: record.ToJson(_jsonSettings),
                tags: record.Tags.ToJson(_jsonSettings));
        }

        /// <inheritdoc />
        public virtual async Task<T> GetAsync<T>(AriesStorage storage, string id) where T : RecordBase, new()
        {
            if (storage.Store is null)
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Store)} which must not be null.");
            }

            if (storage.Store.session == null)
            {
                _ = await AriesAskarStore.StartSessionAsync(storage.Store);
            }

            try
            {
                IntPtr recordHandle = await AriesAskarStore.FetchAsync(
                    storage.Store.session,
                    new T().TypeName,
                    id);

                //empty result for given filter 
                if (recordHandle == new IntPtr())
                {
                    return null;
                }

                SearchOptions options = new();

                string tagsJson = options.RetrieveTags ? await AriesAskarResults.EntryListGetTagsAsync(recordHandle, 0) : null;
                SearchItem item = new()
                {
                    Id = await AriesAskarResults.EntryListGetNameAsync(recordHandle, 0),
                    Type = options.RetrieveType ? await AriesAskarResults.EntryListGetCategoryAsync(recordHandle, 0) : null,
                    Value = options.RetrieveValue ? await AriesAskarResults.EntryListGetValueAsync(recordHandle, 0) : null,
                    Tags = !string.IsNullOrEmpty(tagsJson) ? JsonConvert.DeserializeObject<Dictionary<string, string>>(tagsJson) : null,
                };

                T record = JsonConvert.DeserializeObject<T>(item.Value, _jsonSettings);

                foreach (KeyValuePair<string, string> tag in item.Tags)
                {
                    record.Tags[tag.Key] = tag.Value;
                }

                return record;
            }
            catch (AriesAskarException)
            {
                return null;
            }
        }

        /// <inheritdoc />
        public virtual async Task<bool> DeleteAsync<T>(AriesStorage storage, string id) where T : RecordBase, new()
        {
            if (storage.Store is null)
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Store)} which must not be null.");
            }

            if (storage.Store.session == null)
            {
                _ = await AriesAskarStore.StartSessionAsync(storage.Store);
            }

            try
            {
                T record = await GetAsync<T>(storage, id);
                string typeName = new T().TypeName;

                bool result = await AriesAskarStore.RemoveAsync(
                     session: storage.Store.session,
                     category: typeName,
                     name: id);

                //await AriesAskarStore.CloseAndCommitAsync(wallet.session);

                return result;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Couldn't delete record: {e}");
                return false;
            }
        }

        public virtual async Task AddKeyAsync(AriesStorage storage, IntPtr keyHandle, string did)
        {
            if (storage.Store is null)
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Store)} which must not be null.");
            }

            Debug.WriteLine($"Adding key for did: {did}");

            if (storage.Store.session == null)
            {
                _ = await AriesAskarStore.StartSessionAsync(storage.Store);
            }

            _ = await AriesAskarStore.InsertKeyAsync(
                storage.Store.session,
                keyHandle,
                did);
        }
    }
}
