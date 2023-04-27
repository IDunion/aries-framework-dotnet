using Hyperledger.Aries.Storage.Records;

namespace Hyperledger.Aries.Configuration
{
    /// <summary>
    /// Represents a master secret record in the agency wallet
    /// </summary>
    /// <seealso cref="RecordBase" />
    public class LinkSecretRecord : RecordBase
    {
        /// <inheritdoc />
        public override string TypeName => "AF.LinkSecretRecord";

        /// <summary>
        /// Gets or sets the master secret json.
        /// </summary>
        /// <value>
        /// The master secret json.
        /// </value>
        public string LinkSecretJson { get; set; }

    }
}
