using aries_askar_dotnet.Models;
using Hyperledger.Aries.AskarStore.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Hyperledger.Aries.AskarStore
{
    public interface IStorageRecordService
    {
        Task<bool> AddRecord(Store store, StorageRecord record);
        Task<bool> UpdateRecord(Store store, StorageRecord record, string value, string tags);
        Task<bool> RemoveRecord(Store store, StorageRecord record);
        Task<StorageRecord> GetRecord(Store store, string recordType, string recordId, bool for_update = false);
        Task<StorageRecord> FindRecord(Store store, StorageRecord record, string tag_query, long limit, bool for_update = false);
        Task<IEnumerable<StorageRecord>> FindAllRecord(Store store, StorageRecord record, string tag_query, long limit, bool for_update = false);
    }
}
