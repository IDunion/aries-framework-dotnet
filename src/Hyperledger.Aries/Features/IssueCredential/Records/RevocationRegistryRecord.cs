using Hyperledger.Aries.Storage.Records;

namespace Hyperledger.Aries.Models.Records
{
    /// <summary>
    /// Represents revocation registry record.
    /// </summary>
    public class RevocationRegistryRecord : RecordBase
    {
        /// <summary>
        /// Gets or sets the revocation registry identifier.
        /// </summary>
        /// <value>The revocation registry identifier.</value>
        public string CredentialDefinitionId
        {
            get => Get();
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets the tails file where the registry data is stored.
        /// </summary>
        /// <value>The tails file.</value>
        public string TailsFile
        {
            get => Get();
            set => Set(value);
        }

        /// <summary>
        /// Gets the name of the record type for this object.
        /// </summary>
        /// <value>The type name.</value>
        public override string TypeName => "AF.RevocationRegistryRecord";

        /// <summary>
        /// Gets or sets the public URI tails location.
        /// </summary>
        /// <value>
        /// The tails location.
        /// </value>
        public string TailsLocation { get; set; }

        /// <summary>
        /// Gets or sets the revocation registry definition json.
        /// </summary>
        /// <value>Json of the revocation registry.</value>
        public string RevRegDefJson { get; set; }

        /// <summary>
        /// Gets or sets the private revocation registry definition json.
        /// </summary>
        /// <value>Json of the private revocation registry definition.</value>
        public string RevRegDefPrivateJson { get; set; }

        /// <summary>
        /// Gets or sets the revocation registry json.
        /// </summary>
        /// <value>Json of the revocation registry.</value>
        public string RevRegJson { get; set; }

        /// <summary>
        /// Gets or sets the revocation registry delta json.
        /// </summary>
        /// <value>Json of the revocation registry delta.</value>
        public string RevRegDeltaJson { get; set; }
    }
}
