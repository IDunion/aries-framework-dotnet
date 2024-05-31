using aries_askar_dotnet.AriesAskar;
using aries_askar_dotnet.Models;
using Hyperledger.Aries.AskarStore.Models;
using Hyperledger.Aries.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Hyperledger.Aries.AskarStore
{
    public class DefaultStorageRecordService : IStorageRecordService
    {
        private readonly JsonSerializerSettings _jsonSettings;
        public async Task<Session> CreateSession(Store store)
        {
            Session session = store.CreateSession();
            await session.StartAsync();
            return session;
        }
        public async Task<bool> AddRecord(Store store, StorageRecord record)
        {
            Session session = await CreateSession(store);           
            return await session.InsertAsync(record.Type, record.Id, record.Value, record.Tags);
        }

        public async Task<StorageRecord> FindRecord(Store store, StorageRecord record, string tag_query, long limit, bool for_update = false)
        {
            Session session = await CreateSession(store);
            var items = await session.FetchAllAsync(record.Type, tag_query, limit, for_update);
            var row = JArray.Parse(items.ToJson())[0].ToObject<StorageRecord>();
            return new StorageRecord
            {
                Id = row.Id,
                Type = row.Type,
                Value = row.Value,
                Tags = row.Tags
            };
        }

        public async Task<StorageRecord> GetRecord(Store store, string recordType, string recordId, bool for_update = false)
        {
            Session session = await CreateSession(store);
            var item  = await session.FetchAsync(recordType, recordId, for_update);
            var record = JsonConvert.DeserializeObject<StorageRecord>(item.ToJson(), _jsonSettings); 
            return new StorageRecord
            {
                Id = record.Id,
                Type = record.Type,
                Value = record.Value,
                Tags = record.Tags
            };
        }

        public async Task<bool> RemoveRecord(Store store, StorageRecord record)
        {
            Session session = await CreateSession(store);
            return await session.RemoveAsync(record.Type, record.Id);
        }

        public async Task<bool> UpdateRecord(Store store, StorageRecord record, string value, string tags)
        {
            Session session = await CreateSession(store);
            return await session.ReplaceAsync(record.Type, record.Id, value, tags);
        }

        public async Task<IEnumerable<StorageRecord>> FindAllRecord(Store store, StorageRecord record, string tag_query, long limit, bool for_update = false)
        {
            Session session = await CreateSession(store);
            var items = await session.FetchAllAsync(record.Type, tag_query, limit, for_update);
            var rows = JsonConvert.DeserializeObject<IEnumerable<StorageRecord>>(items.ToJson());
            var results = new List<StorageRecord >();
            foreach (StorageRecord row in rows) {
                results.Add(row);
            }
            return results;
        }
    }
}
