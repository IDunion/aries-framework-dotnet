namespace SdJwt.Abstractions
{
    public interface IVerifier
    {
        public bool VerifyPresentation(string presentation, string issuerJwk);
    }
}
