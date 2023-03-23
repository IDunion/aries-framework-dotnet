using System;
using System.Linq;
using JWT.Builder;
using SdJwt.Abstractions;
using SdJwt.Models;

namespace SdJwt
{
    public class Holder : IHolder
    {
        private readonly IJwtSigningAlgorithmFactory _keySigningAlgorithmFactory;

        public Holder(IJwtSigningAlgorithmFactory keySigningAlgorithmFactory)
        {
            _keySigningAlgorithmFactory = keySigningAlgorithmFactory;
        }
        
        public SdJwtDoc ReceiveCredential(string sdJwt)
        {
            /*
                1. Separate the SD-JWT and the Disclosures in the Combined Format for Issuance.
                2. Hash all of the Disclosures separately.
                3. Find the places in the SD-JWT where the digests of the Disclosures are included. 
                   If any of the digests cannot be found in the SD-JWT, the Holder MUST reject the SD-JWT.
                4. Decode Disclosures and obtain plaintext of the claim values.
             */
            var sdJwtDoc = new SdJwtDoc(sdJwt);

            return sdJwtDoc;
        }

        public string CreatePresentation(SdJwtDoc sdJwt, string[] holderDisclosures, string? holderKey = null, string? nonce = null, string? audience = null)
        {
            /*
                1. Decide which Disclosures to release to the Verifier, obtaining proper End-User consent if necessary.
                2. If Holder Binding is required, create a Holder Binding JWT.
                3. Create the Combined Format for Presentation, including the selected Disclosures and, if applicable, the Holder Binding JWT.
                4. Send the Presentation to the Verifier.
            */
            string presentation = sdJwt.EncodedJwt;

            foreach (var disclosure in sdJwt.Disclosures)
            {
                if (holderDisclosures.Contains(disclosure.Name))
                    presentation += $"~{disclosure.Serialize()}";
            }

            presentation += "~";
            
            // Todo: Add holder binding

            if (holderKey != null && nonce != null && audience != null)
            {
                var jwtBuilder = new JwtBuilder();

                jwtBuilder.AddClaim("nonce", nonce);
                jwtBuilder.AddClaim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                jwtBuilder.AddClaim("aud", audience);

                jwtBuilder.WithAlgorithm(_keySigningAlgorithmFactory.CreateAlgorithm(holderKey));
                jwtBuilder.WithSecret(new[] { "none" });

                presentation += jwtBuilder.Encode();
            }

            return presentation;
        }
    }
}


