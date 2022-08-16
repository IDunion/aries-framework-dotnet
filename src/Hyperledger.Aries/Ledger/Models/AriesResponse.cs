using System;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.Ledger.Models
{
    public class AriesResponse
    {
        //
        // Zusammenfassung:
        //     Gets the identifier.
        //
        // Wert:
        //     The identifier.
        public string Id { get; set; }

        //
        // Zusammenfassung:
        //     Gets the object json.
        //
        // Wert:
        //     The object json.
        public string ObjectJson { get; set; }

        public AriesResponse(string id, string objectJson)
        {
            Id = id;
            ObjectJson = objectJson;
        }
    }
}
