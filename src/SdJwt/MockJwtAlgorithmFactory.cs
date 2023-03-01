using System.Security.Cryptography;
using JWT.Algorithms;
using Microsoft.IdentityModel.Tokens;
using SdJwt.Abstractions;

namespace SdJwt.Tests
{
    public class MockJwtAlgorithmFactory : IJwtAlgorithmFactory
    {
        private const string jwk = "{\n    \"kty\": \"EC\",\n    \"d\": \"Iw6qWZhQ04CtijWzp3q-vGrQfmOcKd1SqjlxMgqzvwA\",\n    \"use\": \"sig\",\n    \"crv\": \"P-256\",\n    \"kid\": \"ECSNPzYd7TefqsBXX6LvfskkZSU=\",\n    \"x\": \"xYrl9sGkLv6_K5xa8jQK1ixQ8FC9pKlkzq2e2Po4_VY\",\n    \"y\": \"a281dDn0k54m0wKl-SfqkXLESv4_G8wZEQWpvKmfO2w\",\n    \"alg\": \"ES256\"\n}";
        
        public IJwtAlgorithm CreateJwtAlgorithm(string keyAlias)
        {
            // Todo: Use hardware key
            var jsonWebKey = new JsonWebKey(jwk);
            var x = Base64UrlEncoder.DecodeBytes(jsonWebKey.X);
            var y = Base64UrlEncoder.DecodeBytes(jsonWebKey.Y);
            var d = Base64UrlEncoder.DecodeBytes(jsonWebKey.D);

            ECDsa ecdsa = ECDsa.Create(new ECParameters()
            {
                Curve = ECCurve.NamedCurves.nistP256,
                D = d,
                Q = new ECPoint {X = x, Y = y}
            })!;
            ECDsaSecurityKey key = new ECDsaSecurityKey(ecdsa);

            return new ES256Algorithm(key.ECDsa, key.ECDsa);
        }
    }
}
