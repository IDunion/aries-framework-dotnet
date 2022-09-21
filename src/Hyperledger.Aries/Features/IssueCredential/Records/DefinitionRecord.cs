using Hyperledger.Aries.Storage.Records;

namespace Hyperledger.Aries.Features.IssueCredential.Records
{
    /// <summary>
    /// Schema definition record.
    /// </summary>
    public class DefinitionRecord : RecordBase
    {
        /// <summary>
        /// Gets or sets the identifier of the schema the definition is derived from.
        /// </summary>
        /// <value>The schema identifier.</value>
        public string SchemaId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this definition supports credential revocation.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this definition supports credential revocation; otherwise, <c>false</c>.
        /// </value>
        public bool SupportsRevocation { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether requests are automatically issued a credential.
        /// </summary>
        /// <value>
        ///   <c>true</c> if [require approval]; otherwise, <c>false</c>.
        /// </value>
        public bool RequireApproval { get; set; }

        /// <summary>
        /// Gets the name of the type.
        /// </summary>
        /// <returns>The type name.</returns>
        public override string TypeName => "AF.CredentialDefinition";

        /// <summary>Gets or sets the maximum credential count.</summary>
        /// <value>The maximum credential count.</value>
        public int MaxCredentialCount { get; set; }

        /// <summary>
        /// Gets a value indicating whether revocation automatic scales.
        /// </summary>
        /// <value>
        ///   <c>true</c> if [revocation automatic scale]; otherwise, <c>false</c>.
        /// </value>
        public bool RevocationAutoScale { get; set; }

        /// <summary>
        /// Gets or sets the current revocation registry identifier.
        /// </summary>
        /// <value>
        /// The current revocation registry identifier.
        /// </value>
        public string CurrentRevocationRegistryId { get; set; }

        /// <summary>
        /// Gets or sets the issuer DID.
        /// </summary>
        /// <value>
        /// The issuer did.
        /// </value>
        public string IssuerDid { get; set; }

        /// <summary>
        /// Gets or sets the json of the credential definition.
        /// </summary>
        /// <value>Json of credential definition.</value>
        public string CredDefJson { get; set; }

        /// <summary>
        /// Gets or sets the private definition json.
        /// </summary>
        /// <value>Json of the private definition.</value>
        public string PrivateJson { get; set; }

        /// <summary>
        /// Gets or sets the key correctness proof json.
        /// </summary>
        /// <value>Json of the key correctness proof.</value>
        public string KeyCorrectnesProofJson { get; set; }
    }
}
