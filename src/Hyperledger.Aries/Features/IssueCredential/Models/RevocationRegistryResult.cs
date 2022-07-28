using System;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.Features.IssueCredential.Models
{
    public class RevocationRegistryResult
    {
        //
        // Zusammenfassung:
        //     Gets the rev reg identifier.
        //
        // Wert:
        //     The rev reg identifier.
        public string RevRegId { get; set; }

        //
        // Zusammenfassung:
        //     Gets the rev reg def json.
        //
        // Wert:
        //     The rev reg def json.
        public string RevRegDefJson { get; set; }

        //
        // Zusammenfassung:
        //     Gets the rev reg def json.
        //
        // Wert:
        //     The rev reg def json.
        public string RevRegDefPvtJson { get; set; }

        //
        // Zusammenfassung:
        //     Gets the rev reg entry json.
        //
        // Wert:
        //     The rev reg entry json.
        public string RevRegEntryJson { get; set; }

        public RevocationRegistryResult(
            string revocationRegistryId, 
            string revocationRegistryDefinitionJson, 
            string revocationRegistryDefinitionPrivateJson, 
            string revocationRegistryJson)
        {
            RevRegId = revocationRegistryId;
            RevRegDefJson = revocationRegistryDefinitionJson;
            RevRegDefPvtJson = revocationRegistryDefinitionPrivateJson;
            RevRegEntryJson = revocationRegistryJson;
        }
    }
}
