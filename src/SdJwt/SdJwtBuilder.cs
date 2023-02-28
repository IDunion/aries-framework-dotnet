using System;
using System.Collections.Generic;
using System.Linq;
using JWT.Algorithms;
using JWT.Builder;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using SdJwt.Models;

namespace SdJwt
{
    public class SdJwtBuilder
    {
        private const string SD_CLAIMS = "_sd";
        private const string SD_ALG = "_sd_alg";
        private const string CNF = "cnf"; // Confirmation claim https://www.rfc-editor.org/rfc/inline-errata/rfc7800.html
        
        private readonly List<Claim> _addedClaims = new List<Claim>();
        private IJwtAlgorithm? _algorithm;
        private JsonWebKey? _holderKey;
        private string[]? _secret;

        public string Build()
        {
            JwtBuilder jwtBuilder = JwtBuilder.Create();
            List<Disclosure> disclosures = new List<Disclosure>();
            JArray sdClaims = new JArray();

            foreach (var nonSdClaim in _addedClaims.Where(cl => cl.NonDisclosable == false))
            {
                jwtBuilder.AddClaim(nonSdClaim.Name, nonSdClaim.Value);
            }
            foreach (var sdClaim in _addedClaims.Where(cl => cl.NonDisclosable == true))
            {
                var disclosure = new Disclosure(sdClaim.Name, sdClaim.Value);
                disclosures.Add(disclosure);
                sdClaims.Add(disclosure.GetDigest());
            }
            jwtBuilder.AddClaim(SD_CLAIMS, sdClaims);
            jwtBuilder.AddClaim(SD_ALG, "sha-256");
            
            if (_holderKey != null)
            {
                // https://mkjwk.org/
                jwtBuilder.AddClaim(CNF, new { jwk = new
                {
                    kty = _holderKey.Kty,
                    use = _holderKey.Use,
                    crv = _holderKey.Crv,
                    kid = _holderKey.Kid,
                    x = _holderKey.X,
                    y = _holderKey.Y,
                    alg = _holderKey.Alg,
                }});
            }

            jwtBuilder.WithAlgorithm(_algorithm);
            jwtBuilder.WithSecret(_secret);
            
            return jwtBuilder.Encode() + "~" + string.Join('~', disclosures.Select( x => x.Serialize())) + "~";
        }
        
        public void AddClaim(Claim claim)
        {
            if (_addedClaims.Find(cl => cl.Name.Equals(claim.Name, StringComparison.Ordinal)) != null)
                throw new ArgumentException($"A claim with this name was already added: {claim.Name}");
            
            _addedClaims.Add(claim);
        }

        public void AddHolderBinding(JsonWebKey jsonWebKey)
        {
            _holderKey = jsonWebKey;
        }

        public void AddAlgorithm(IJwtAlgorithm algorithm)
        {
            _algorithm = algorithm;
        }

        public void AddSecret(string[] secret)
        {
            _secret = secret;
        }
    }
}


