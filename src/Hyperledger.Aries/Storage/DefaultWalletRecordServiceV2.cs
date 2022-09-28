using aries_askar_dotnet;
using aries_askar_dotnet.AriesAskar;
using aries_askar_dotnet.Models;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Common;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.PresentProof;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Aries.Storage.Records;
using Hyperledger.Aries.Storage.Records.Search;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AriesAskarErrorCode = aries_askar_dotnet.ErrorCode;
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
            await Validate(storage);

            try
            {
                Debug.WriteLine($"Adding record of type {record.TypeName} with Id {record.Id}");
                record.CreatedAtUtc = DateTime.UtcNow;

                _ = await AriesAskarStore.InsertAsync(
                    session: storage.Store.session,
                    category: record.TypeName,
                    name: record.Id,
                    value: record.ToJson(_jsonSettings),
                    tags: record.Tags.ToJson());
            }
            catch (AriesAskarException e)
            {
                if (e.errorCode == AriesAskarErrorCode.Duplicate)
                {
                    Debug.WriteLine($"Record of type '{record.TypeName}' already exists in store for id: {record.Id}");
                }
                else
                {
                    throw new AriesAskarException(e.Message, e.errorCode);
                }
            }
            finally
            {
                _ = await AriesAskarStore.CloseAndCommitAsync(storage.Store.session);
            }
        }

        /// <inheritdoc />
        public virtual async Task<List<T>> SearchAsync<T>(AriesStorage storage, ISearchQuery query, SearchOptions options, int count, int skip)
            where T : RecordBase, new()
        {
            await Validate(storage);

            try
            {
                options ??= new SearchOptions();
                string filter = (query ?? SearchQuery.Empty).ToJson();
                Scan search = await AriesAskarStore.StartScanAsync(
                    store: storage.Store,
                    category: new T().TypeName,
                    tagFilter: filter,
                    offset: skip,
                    limit: count);
                IntPtr entryListHandle = await AriesAskarStore.NextAsync(search);

                //empty result for given filter 
                if (entryListHandle == new IntPtr())
                {
                    Debug.WriteLine($"Record of type {new T().TypeName} doesn't exist in store for tagFilter: {filter}");
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
            catch (AriesAskarException e)
            {
                throw new AriesAskarException(e.Message, e.errorCode);
            }
            finally
            {
                _ = await AriesAskarStore.CloseAndCommitAsync(storage.Store.session);
            }
        }

        /// <inheritdoc />
        public virtual async Task UpdateAsync(AriesStorage storage, RecordBase record)
        {
            await Validate(storage);

            record.UpdatedAtUtc = DateTime.UtcNow;

            try
            {
                _ = await AriesAskarStore.ReplaceAsync(
                    session: storage.Store.session,
                    category: record.TypeName,
                    name: record.Id,
                    value: record.ToJson(_jsonSettings),
                    tags: record.Tags.ToJson(_jsonSettings));
            }
            catch (AriesAskarException e)
            {
                throw new AriesAskarException(e.Message, e.errorCode);
            }
            finally
            {
                _ = await AriesAskarStore.CloseAndCommitAsync(storage.Store.session);
            }
        }

        /// <inheritdoc />
        public virtual async Task<T> GetAsync<T>(AriesStorage storage, string id) where T : RecordBase, new()
        {
            await Validate(storage);

            try
            {
                IntPtr recordHandle = await AriesAskarStore.FetchAsync(
                    storage.Store.session,
                    new T().TypeName,
                    id);

                //empty result for given filter 
                if (recordHandle == default)
                {
                    Debug.WriteLine($"Record of type {new T().TypeName} doesn't exist in store for id: {id}");
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
            catch (AriesAskarException e)
            {
                if (e.errorCode == AriesAskarErrorCode.Input)
                {
                    Debug.WriteLine($"Record of type {new T().TypeName} doesn't exist in store for id: {id}");
                    return null;
                }
                else
                {
                    throw new AriesAskarException(e.Message, e.errorCode);
                }
            }
            finally
            {
                _ = await AriesAskarStore.CloseAndCommitAsync(storage.Store.session);
            }
        }

        /// <inheritdoc />
        public virtual async Task<bool> DeleteAsync<T>(AriesStorage storage, string id) where T : RecordBase, new()
        {
            await Validate(storage);

            try
            {
                bool result = await storage.Store.session.RemoveAsync(new T().TypeName, id);

                return result;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Couldn't delete record: {e}");
                return false;
            }
            finally
            {
                try
                {
                    _ = await AriesAskarStore.CloseAndCommitAsync(storage.Store.session);
                }
                catch
                {

                }
            }
        }

        public virtual async Task AddKeyAsync(AriesStorage storage, IntPtr keyHandle, string myVerkey)
        {
            await Validate(storage);

            try
            {
                Debug.WriteLine($"Adding key for verkey: {myVerkey}");

                _ = await AriesAskarStore.InsertKeyAsync(
                    storage.Store.session,
                    keyHandle,
                    myVerkey);
            }
            catch (AriesAskarException e)
            {
                if (e.errorCode == AriesAskarErrorCode.Duplicate)
                {
                    Debug.WriteLine($"Keypair already exists in store for verkey: {myVerkey}");
                }
                else
                {
                    throw new AriesAskarException(e.Message, e.errorCode);
                }
            }
            finally
            {
                _ = await AriesAskarStore.CloseAndCommitAsync(storage.Store.session);
            }
        }

        public virtual async Task<IntPtr> GetKeyAsync(AriesStorage storage, string myVerkey)
        {
            await Validate(storage);

            try
            {
                Debug.WriteLine($"Getting keypair for verkey: {myVerkey}");

                IntPtr keyEntryListHandle = await AriesAskarStore.FetchKeyAsync(storage.Store.session, myVerkey);
                return await AriesAskarResults.LoadLocalKeyHandleFromKeyEntryListAsync(keyEntryListHandle, 0);
            }
            catch (AriesAskarException e)
            {
                if (e.errorCode == AriesAskarErrorCode.Input)
                {
                    Debug.WriteLine($"Keypair doesn't exist in store for verkey: {myVerkey}");
                    return new IntPtr();
                }
                else
                {
                    throw new AriesAskarException(e.Message, e.errorCode);
                }
            }
            finally
            {
                _ = await AriesAskarStore.CloseAndCommitAsync(storage.Store.session);
            }
        }

        public virtual async Task<IntPtr> GetKeyAsync(Store store, string myVerkey)
        {
            await Validate(store);

            try
            {
                Debug.WriteLine($"Getting keypair for verkey: {myVerkey}");

                IntPtr keyEntryListHandle = await AriesAskarStore.FetchKeyAsync(store.session, myVerkey);
                return await AriesAskarResults.LoadLocalKeyHandleFromKeyEntryListAsync(keyEntryListHandle, 0);
            }
            catch (AriesAskarException e)
            {
                if (e.errorCode == AriesAskarErrorCode.Input)
                {
                    Debug.WriteLine($"Keypair doesn't exist in store for verkey: {myVerkey}");
                    return new IntPtr();
                }
                else
                {
                    throw new AriesAskarException(e.Message, e.errorCode);
                }
            }
            finally
            {
                _ = await AriesAskarStore.CloseAndCommitAsync(store.session);
            }
        }

        private async Task Validate(AriesStorage storage)
        {
            if (storage.Store is null)
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Store)} which must not be null.");
            }

            if (storage.Store.session == null || storage.Store.session.sessionHandle == default)
            {
                _ = await AriesAskarStore.StartSessionAsync(storage.Store);
            }
        }

        private async Task Validate(Store store)
        {
            if (store is null)
            {
                throw new AriesFrameworkException(ErrorCode.InvalidStorage, $"You need a storage of type {typeof(Store)} which must not be null.");
            }

            if (store.session == null || store.session.sessionHandle == default)
            {
                _ = await AriesAskarStore.StartSessionAsync(store);
            }
        }
    }
}
