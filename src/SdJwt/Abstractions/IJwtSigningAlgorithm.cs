using JWT.Algorithms;

namespace SdJwt.Abstractions
{
    public interface IJwtSigningAlgorithm : IJwtAlgorithm
    {
        /// <summary>
        /// Returns the public key information as jwk
        /// </summary>
        /// <returns>The JsonWebKey.</returns>
        string GetJwk();
    }
}
