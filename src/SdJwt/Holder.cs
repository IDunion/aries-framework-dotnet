using System;
using System.Linq;
using JWT.Builder;
using SdJwt.Abstractions;
using SdJwt.Models;

namespace SdJwt
{
    public class Holder : IHolder
    {
        private readonly IJwtAlgorithmFactory _keyAlgorithmFactory;

        public Holder(IJwtAlgorithmFactory keyAlgorithmFactory)
        {
            _keyAlgorithmFactory = keyAlgorithmFactory;
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

                jwtBuilder.WithAlgorithm(_keyAlgorithmFactory.CreateJwtAlgorithm(holderKey));

                presentation += jwtBuilder.Encode();
            }

            return presentation;
        }
    }
    
    // // Add holder binding
    // presentation += "~";
    //
    // if (holderKey != null && nonce != null && audience != null)
    // {
    //     var payload = new Dictionary<string, object>()
    //     {
    //         { "nonce", "XZOUco1u_gEPknxS78sWWg" },
    //         { "aud", "https://example.com/verifier" },
    //         { "iat", "1676965944" }
    //     };
    //             
    //     byte[] x = { 4, 114, 29, 223, 58, 3, 191, 170, 67, 128, 229, 33, 242, 178, 157, 150, 133, 25, 209, 139, 166, 69, 55, 26, 84, 48, 169, 165, 67, 232, 98, 9 };
    //     byte[] y = { 131, 116, 8, 14, 22, 150, 18, 75, 24, 181, 159, 78, 90, 51, 71, 159, 214, 186, 250, 47, 207, 246, 142, 127, 54, 183, 72, 72, 253, 21, 88, 53 };
    //     byte[] d = { 42, 148, 231, 48, 225, 196, 166, 201, 23, 190, 229, 199, 20, 39, 226, 70, 209, 148, 29, 70, 125, 14, 174, 66, 9, 198, 80, 251, 95, 107, 98, 206 };
    //
    //     var privateKey = EccKey.New(x, y, d);
    //     var confirmationJwt = Jose.JWT.Encode(payload, privateKey, JwsAlgorithm.ES256);
    //
    //     presentation += confirmationJwt;
    // }
}


