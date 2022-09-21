namespace Hyperledger.Aries.Ledger.Models
{
    public class AriesRegistryResponse
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

        //
        // Zusammenfassung:
        //     Gets the timestamp.
        //
        // Wert:
        //     The timestamp.
        public ulong Timestamp { get; set; }

        public AriesRegistryResponse(string id, string objectJson, ulong timestamp)
        {
            Id = id;
            ObjectJson = objectJson;
            Timestamp = timestamp;
        }
    }
}
