using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;

namespace SdJwt.Models 
{
    public class SdJwtDoc
    {
        public List<Disclosure> Disclosures { get; }
        public string EncodedJwt { get; }

        private readonly string _encodedSdJwt;

        private readonly string _jwtContent;

        private string? _verifiedJwtContent;

        public SdJwtDoc(string issuedSdJwt)
        {
            _encodedSdJwt = issuedSdJwt;
            
            var items = issuedSdJwt.Split('~');

            EncodedJwt = items.First();
            _jwtContent = Base64UrlEncoder.Decode(EncodedJwt.Split('.')[1]);
            Disclosures = items[1..].Select(Disclosure.Deserialize).ToList();
            
            Verify();
        }

        private void Verify()
        {
            JObject jObject = JObject.Parse(_jwtContent);
            var hashedDisclosures = Disclosures.ToDictionary(x => x.GetDigest());

            var sdProperties = FindPropertiesByName(jObject, "_sd");
            
            foreach (var sdProperty in sdProperties)
            {
                if (sdProperty.Value is JArray array)
                {
                    foreach (var jtoken in array)
                    {
                        string hash = jtoken.Value<string>()!;
                        if (hashedDisclosures.Keys.Contains(hash))
                        {
                            var disclosure = hashedDisclosures[hash];
                            var newJProperty = new JProperty("~~sd~~" + disclosure.Name, disclosure.Value);
                            sdProperty.Parent!.Add(newJProperty);
                            
                            hashedDisclosures.Remove(hash);
                        }
                    }
                }
            }

            if (hashedDisclosures.Count != 0) throw new Exception("Invalid sd jwt");

            _verifiedJwtContent = jObject.ToString();
        }
        
        private List<JProperty> FindPropertiesByName(JToken token, string propertyName)
        {
            List<JProperty> matchingProperties = new List<JProperty>();

            if (token.Type == JTokenType.Object)
            {
                foreach (JProperty property in token.Children<JProperty>())
                {
                    if (property.Name == propertyName)
                    {
                        matchingProperties.Add(property);
                    }

                    matchingProperties.AddRange(FindPropertiesByName(property.Value, propertyName));
                }
            }
            else if (token.Type == JTokenType.Array)
            {
                foreach (JToken child in token.Children())
                {
                    matchingProperties.AddRange(FindPropertiesByName(child, propertyName));
                }
            }

            return matchingProperties;
        }
    }
}
