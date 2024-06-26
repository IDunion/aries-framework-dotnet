using System;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.AskarStore.Models
{
    public class StorageRecord
    {
        public string Id { get; set; }
        public string Value { get; set; }
        public string Type { get; set; }
        public string Tags { get; set; }
    }
}
