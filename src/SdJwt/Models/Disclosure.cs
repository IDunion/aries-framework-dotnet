using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("SdJwt.Tests")]
namespace SdJwt.Models
{
    public class Disclosure
    {
        public string Salt;
        
        public string Name;
        
        public object Value;
    
        private string? _base64UrlEncoded;
    
        public Disclosure(string name, object value)
        {
            var randomBytes = new byte[16]; 
            RandomNumberGenerator.Create().GetBytes(randomBytes);
            
            Salt = Base64UrlEncoder.Encode(randomBytes);
            Name = name;
            Value = value;
        }
        
        internal Disclosure()
        {
        }
    
        public static Disclosure Deserialize(string input)
        {
            var decodedInput = Base64UrlEncoder.Decode(input);
            var array = JArray.Parse(decodedInput) ?? throw new SerializationException($"Could not deserialize given disclosure {input}");
            return new Disclosure
            {
                Salt = array[0].Value<string>() ?? throw new SerializationException("Salt could not be deserialized"),
                Name = array[1].Value<string>() ?? throw new SerializationException("Name could not be deserialized"),
                Value = array[2],
                _base64UrlEncoded = input
            };
        }
        
        public string Serialize()
        {
            if (_base64UrlEncoded != null) return _base64UrlEncoded;
            
            var array = new[] { Salt, Name, Value };
            var json = JsonSerializer.SerializeToUtf8Bytes(array);
            return Base64UrlEncoder.Encode(json);
        }
    
        /// <summary>
        /// Get the hash of the disclosure
        /// </summary>
        /// <returns>The base64url encoded hash of the base64url encoded disclosure json object</returns>
        public string GetDigest()
        {
            var hashValue = _base64UrlEncoded != null ? ComputeDigest(_base64UrlEncoded) : ComputeDigest(Serialize());
            return Base64UrlEncoder.Encode(hashValue);
        }
        
        private byte[] ComputeDigest(string input)
        {
            using var sha26 = SHA256.Create();
            return sha26.ComputeHash(Encoding.ASCII.GetBytes(input));
        }
    }
}


