using JWT.Algorithms;

namespace SdJwt.Abstractions
{
    public interface IJwtAlgorithmFactory
    {
        public IJwtAlgorithm CreateJwtAlgorithm(string keyAlias);
    }
}
