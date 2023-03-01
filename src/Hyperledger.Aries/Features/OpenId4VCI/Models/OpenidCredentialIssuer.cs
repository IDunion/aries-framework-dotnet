using Newtonsoft.Json;

namespace Hyperledger.Aries.Features.OpenId4VCI.Models
{

    public class OpenidCredentialIssuer
    {
        [JsonProperty("credential_issuer")]
        public string CredentialIssuer { get; set; }
        [JsonProperty("credential_endpoint")]
        public string CredentialEndpoint { get; set; }
        [JsonProperty("display")]
        public Display[] Display { get; set; }
        [JsonProperty("credentials_supported")]
        public Credentials_Supported[] CredentialsSupported { get; set; }
    }

    public class Credentials_Supported
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("format")]
        public string Format { get; set; }
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("cryptographic_binding_methods_supported")]
        public string[] CryptographicBindingMethodsSupported { get; set; }
        [JsonProperty("cryptographic_suites_supported")]
        public string[] CryptographicSuitesSupported { get; set; }
        [JsonProperty("display")]
        public Display[] Display { get; set; }
        [JsonProperty("credentialSubject")]
        public CredentialSubject CredentialSubject { get; set; }
    }

    public class CredentialSubject
    {
        [JsonProperty("given_name")]
        public Given_Name GivenName { get; set; }
        [JsonProperty("last_name")]
        public Last_Name LastName { get; set; }
        [JsonProperty("email")]
        public Email Email { get; set; }
    }

    public class Given_Name
    {
        [JsonProperty("display")]
        public Display[] Display { get; set; }
    }

    public class Last_Name
    {
        [JsonProperty("display")]
        public Display[] Display { get; set; }
    }

    public class Email
    {
        [JsonProperty("display")]
        public Display[] Display { get; set; }
    }

    public class Display
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("local")]
        public string Locale { get; set; }
        [JsonProperty("logo")]
        public Logo Logo { get; set; }
        [JsonProperty("background_color")]
        public string BackgroundColor { get; set; }
        [JsonProperty("text_color")]
        public string TextColor { get; set; }
    }

    public class Logo
    {
        [JsonProperty("url")]
        public string Url { get; set; }
        [JsonProperty("alternative_text")]
        public string AlternativeText { get; set; }
    }

}
