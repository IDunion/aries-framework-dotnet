using Newtonsoft.Json;
using System.Collections.Generic;

namespace Hyperledger.Aries.Storage.Records.Search
{
    /// <summary>
    /// Search record result.
    /// </summary>
    public class SearchResult
    {
        /// <summary>
        /// Gets or sets the resulting records.
        /// </summary>
        /// <value>The resulting records.</value>
        [JsonProperty("records")]
        public List<SearchItem> Records { get; set; }
    }
}
