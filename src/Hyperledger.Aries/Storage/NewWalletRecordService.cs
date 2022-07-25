using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.PresentProof;
using Hyperledger.Indy.NonSecretsApi;
using Hyperledger.Indy.WalletApi;
using AriesAskarStore = aries_askar_dotnet.AriesAskar.StoreApi;
using AriesAskarResults = aries_askar_dotnet.AriesAskar.ResultListApi;
using Newtonsoft.Json;
using aries_askar_dotnet.Models;

namespace Hyperledger.Aries.Storage
{
    /// <inheritdoc />
    public class NewWalletRecordService : INewWalletRecordService
    {
        private readonly JsonSerializerSettings _jsonSettings;

        /// <summary>Initializes a new instance of the <see cref="DefaultWalletRecordService"/> class.</summary>
        public NewWalletRecordService()
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
        public virtual async Task AddAsync<T>(Store wallet, T record)
            where T : RecordBase, new()
        {
            Debug.WriteLine($"Adding record of type {record.TypeName} with Id {record.Id}");

            record.CreatedAtUtc = DateTime.UtcNow;

            if (wallet.session == null)
            {
                _ = await AriesAskarStore.StartSessionAsync(wallet);
            }

            await AriesAskarStore.InsertAsync(
                session : wallet.session,
                category : record.TypeName,
                name : record.Id,
                value : record.ToJson(_jsonSettings),
                tags : record.Tags.ToJson());
        }

        /// <inheritdoc />
        // TODO : ??? - change SearchAsync to use the aries-askar methods
        public virtual async Task<List<T>> SearchAsync<T>(Store wallet, ISearchQuery query, SearchOptions options, int count, int skip)
            where T : RecordBase, new()
        {
            using (var search = await NonSecrets.OpenSearchAsync(wallet, new T().TypeName,
                (query ?? SearchQuery.Empty).ToJson(),
                (options ?? new SearchOptions()).ToJson()))
            {
                if (skip > 0)
                {
                    await search.NextAsync(wallet, skip);
                }
                var result = JsonConvert.DeserializeObject<SearchResult>(await search.NextAsync(wallet, count), _jsonSettings);

                return result.Records?
                           .Select(x =>
                           {
                               var record = JsonConvert.DeserializeObject<T>(x.Value, _jsonSettings);
                               foreach (var tag in x.Tags)
                                   record.Tags[tag.Key] = tag.Value;
                               return record;
                           })
                           .ToList()
                       ?? new List<T>();
            }
        }

        /// <inheritdoc />
        public virtual async Task UpdateAsync(Store wallet, RecordBase record)
        {
            record.UpdatedAtUtc = DateTime.UtcNow;
            if (wallet.session == null)
            {
                _ = await AriesAskarStore.StartSessionAsync(wallet);
            }

            await AriesAskarStore.ReplaceAsync(
                session : wallet.session,
                category : record.TypeName,
                name : record.Id,
                value : record.ToJson(_jsonSettings),
                tags : record.Tags.ToJson(_jsonSettings));
        }

        /// <inheritdoc />
        public virtual async Task<T> GetAsync<T>(Store wallet, string id) where T : RecordBase, new()
        {
            if (wallet.session == null)
            {
                _ = await AriesAskarStore.StartSessionAsync(wallet);
            }

            try
            {
                IntPtr recordHandle = await AriesAskarStore.FetchAsync(
                    wallet.session,
                    new T().TypeName,
                    id);
                if (recordHandle == new IntPtr()) 
                    return null;

                List<string> records = new();
                SearchOptions options = new();
                int numRecords = await AriesAskarResults.EntryListCountAsync(recordHandle);
                for (int i = 0; i<numRecords; i++)
                {
                    string type = options.RetrieveType ? await AriesAskarResults.EntryListGetCategoryAsync(recordHandle, i) : null;
                    string value = options.RetrieveValue ? await AriesAskarResults.EntryListGetValueAsync(recordHandle, i) : null;
                    string tags = options.RetrieveTags ? await AriesAskarResults.EntryListGetTagsAsync(recordHandle, i) : null;
                    records.Add(JsonConvert.SerializeObject(new
                    {
                        id = await AriesAskarResults.EntryListGetNameAsync(recordHandle, i),
                        type,
                        value,
                        tags,
                    }));
                }
                SearchItem item = JsonConvert.DeserializeObject<SearchItem>(records.First(), _jsonSettings);

                T record = JsonConvert.DeserializeObject<T>(item.Value, _jsonSettings);

                foreach (var tag in item.Tags)
                    record.Tags[tag.Key] = tag.Value;

                return record;
            }
            catch (WalletItemNotFoundException)
            {
                return null;
            }
        }

        /// <inheritdoc />
        public virtual async Task<bool> DeleteAsync<T>(Store wallet, string id) where T : RecordBase, new()
        {
            if (wallet.session == null)
            {
                _ = await AriesAskarStore.StartSessionAsync(wallet);
            }

            try
            {
                var record = await GetAsync<T>(wallet, id);
                var typeName = new T().TypeName;

                await AriesAskarStore.RemoveAllAsync(
                    session : wallet.session, 
                    category : typeName, 
                    tagFilter : record.Tags.Select(x => x.Key).ToArray().ToJson());

                bool result = await AriesAskarStore.RemoveAsync(
                    session : wallet.session, 
                    category : typeName, 
                    name : id);

                //await AriesAskarStore.CloseAndCommitAsync(wallet.session);

                return result;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Couldn't delete record: {e}");
                return false;
            }
        }
    }
}
