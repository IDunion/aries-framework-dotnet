using Hyperledger.Aries.Storage.Models;
using Hyperledger.Aries.Storage.Records;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Storage
{
    /// <summary>
    /// Wallet record service.
    /// </summary>
    public interface IWalletRecordService
    {
        /// <summary>
        /// Adds the record async.
        /// </summary>
        /// <returns>The record async.</returns>
        /// <param name="storage">The indy-sdk or aries-askar Wallet.</param>
        /// <param name="record">Record.</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        Task AddAsync<T>(AriesStorage storage, T record) where T : RecordBase, new();

        /// <summary>
        /// Searchs the records async.
        /// </summary>
        /// <returns>The records async.</returns>
        /// <param name="storage">The indy-sdk or aries-askar Wallet.</param>
        /// <param name="query">Query.</param>
        /// <param name="options">Options.</param>
        /// <param name="count">The number of items to return</param>
        /// <param name="skip">The number of items to skip</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        Task<List<T>> SearchAsync<T>(AriesStorage storage, ISearchQuery query = null, SearchOptions options = null, int count = 10, int skip = 0) where T : RecordBase, new();

        /// <summary>
        /// Updates the record async.
        /// </summary>
        /// <returns>The record async.</returns>
        /// <param name="storage">The indy-sdk or aries-askar Wallet.</param>
        /// <param name="record">Credential record.</param>
        Task UpdateAsync(AriesStorage storage, RecordBase record);

        /// <summary>
        /// Gets the record async.
        /// </summary>
        /// <returns>The record async.</returns>
        /// <param name="storage">The indy-sdk or aries-askar Wallet.</param>
        /// <param name="id">Identifier.</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        Task<T> GetAsync<T>(AriesStorage storage, string id) where T : RecordBase, new();

        /// <summary>
        /// Deletes the record async.
        /// </summary>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        /// <param name="storage">The indy-sdk or aries-askar Wallet.</param>
        /// <param name="id">Record Identifier.</param>
        /// <returns>Boolean status indicating if the removal succeed</returns>
        Task<bool> DeleteAsync<T>(AriesStorage storage, string id) where T : RecordBase, new();

        /// <summary>
        /// Adds a key for a given verkey.
        /// </summary>
        /// <param name="storage">The indy-sdk or aries-askar Wallet.</param>
        /// <param name="keyHandle"></param>
        /// <param name="myVerkey"></param>
        Task AddKeyAsync(AriesStorage storage, IntPtr keyHandle, string myVerkey);

        /// <summary>
        /// Gets a key for a given verkey.
        /// </summary>
        /// <param name="storage">The indy-sdk or aries-askar Wallet.</param>
        /// <param name="myVerkey"></param>
        Task<IntPtr> GetKeyAsync(AriesStorage storage, string myVerkey);
    }
}
