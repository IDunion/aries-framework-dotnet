using System.Security.Cryptography;
using JWT.Algorithms;
using Microsoft.IdentityModel.Tokens;
using SdJwt.Abstractions;

namespace SdJwt.Tests
{
    public class MockJwtAlgorithmFactory : IJwtAlgorithmFactory
    {
        public IJwtAlgorithm CreateJwtAlgorithm(string keyAlias)
        {
            // Todo: Use hardware key
            ECDsa ecdsa = ECDsa.Create()!;
            ECDsaSecurityKey key = new ECDsaSecurityKey(ecdsa);

            return new ES256Algorithm(key.ECDsa, key.ECDsa);
        }
    }
}
