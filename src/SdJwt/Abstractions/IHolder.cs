using SdJwt.Models;

namespace SdJwt.Abstractions
{
    public interface IHolder
    {
        /// <summary>
        /// Receive and parse the combined issuance format of an SD-JWT
        /// </summary>
        /// <param name="sdJwt">The serialized issuance format of an SD-JWT</param>
        /// <returns>The parsed SdJwtDoc.</returns>
        public SdJwtDoc ReceiveCredential(string sdJwt);

        /// <summary>
        /// Create the combined presentation format of an SD-JWT
        /// </summary>
        /// <param name="sdJwt">The SD-JWT to be presented</param>
        /// <param name="holderDisclosures">The array of hashes which should be presented</param>
        /// <param name="holderKey">The reference to an holder key</param>
        /// <param name="nonce">The nonce for holder confirmation</param>
        /// <param name="audience">The audience of the presentation</param>
        /// <returns>The serialized combined presentation format.</returns>
        public string CreatePresentation(SdJwtDoc sdJwt, string[] holderDisclosures, string? holderKey = null, string? nonce = null, string? audience = null);
    }
}
