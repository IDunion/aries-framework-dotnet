namespace SdJwt.Abstractions
{
    public interface IJwtSigningAlgorithmFactory
    {
        public IJwtSigningAlgorithm CreateAlgorithm(string keyAlias);
    }
}
