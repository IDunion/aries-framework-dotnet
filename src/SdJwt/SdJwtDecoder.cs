using JWT.Algorithms;
using JWT.Builder;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using SdJwt.Models;

namespace SdJwt
{
    public class SdJwtDecoder
    {
        private string _jwt;
        private string _json;
        private List<Disclosure> _disclosures;
    
        public SdJwtDecoder(string presentation)
        {
            var ps = presentation.Split('~');
    
            _jwt = ps.First();
            _json = Base64UrlEncoder.Decode(_jwt.Split('.')[1]);
            _disclosures = ps[1..^1].Select(Disclosure.Deserialize).ToList();
        }
    
        public string Verify(IJwtAlgorithm algorithm)
        {
            string json = JwtBuilder.Create()
                .MustVerifySignature()
                .WithAlgorithm(algorithm)
                .Decode(_jwt);
    
            JObject jObject = JObject.Parse(json);
            if (jObject.ContainsKey("_sd") && jObject["_sd"] is JArray array)
            {
                foreach (var disclosure in _disclosures)
                {
                    var token = array.First(hash => hash.Value<string>() == disclosure.GetDigest());
                    token.Remove();
                    
                    jObject.Add(disclosure.Name, disclosure.Value as JToken);
                }
            }
    
            return jObject.ToString();
        }
    }
}
