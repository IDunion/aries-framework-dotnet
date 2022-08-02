using Hyperledger.Aries.Storage;

namespace Hyperledger.Aries.Configuration
{
    /// <summary>
    /// Represents a master secret record in the agency wallet
    /// </summary>
    /// <seealso cref="RecordBase" />
    public class MasterSecretRecord : RecordBase
    {
        /// <inheritdoc />
        public override string TypeName => "AF.MasterSecretRecord";

        /// <summary>
        /// Gets or sets the master secret json.
        /// </summary>
        /// <value>
        /// The master secret json.
        /// </value>
        public string MasterSecretJson { get; set; }

    }
}
