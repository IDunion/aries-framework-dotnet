using NUnit.Framework;

namespace SdJwt.Tests
{
    public class VerifierTests
    {
        private const string w3c = @"{
            ""iss"": ""https://example.com"",
            ""jti"": ""http://example.com/credentials/3732"",
            ""nbf"": 1541493724,
            ""iat"": 1541493724,
            ""cnf"": {
                ""jwk"": {
                    ""kty"": ""RSA"",
                    ""n"": ""0vx7agoebGcQSuuPiLJXZptN9nndrQmbXEps2aiAFbWhM78LhWx4cbbf
                    AAtVT86zwu1RK7aPFFxuhDR1L6tSoc_BJECPebWKRXjBZCiFV4n3oknjhMst
                    n64tZ_2W-5JsGY4Hc5n9yBXArwl93lqt7_RN5w6Cf0h4QyQ5v-65YGjQR0_F
                        DW2QvzqY368QQMicAtaSqzs8KJZgnYb9c7d0zgdAZHzu6qMQvRL5hajrn1n9
                    1CbOpbISD08qNLyrdkt-bFTWhAI4vMQFh6WeZu0fM4lFd2NcRwr3XPksINHa
                    Q-G_xBniIqbw0Ls1jF44-csFCur-kEgU8awapJzKnqDKgw"",
                    ""e"": ""AQAB""
                }
            },
            ""type"": ""IdentityCredential"",
            ""credentialSubject"": {
                ""given_name"": ""John"",
                ""family_name"": ""Doe"",
                ""email"": ""johndoe@example.com"",
                ""phone_number"": ""+1-202-555-0101"",
                ""address"": {
                    ""street_address"": ""123 Main St"",
                    ""locality"": ""Anytown"",
                    ""region"": ""Anystate"",
                    ""country"": ""US""
                },
                ""birthdate"": ""1940-01-01"",
                ""is_over_18"": true,
                ""is_over_21"": true,
                ""is_over_65"": true
            }
        }";
        
        private const string _expectedPresentation =
            "eyJhbGciOiAiUlMyNTYiLCAia2lkIjogImNBRUlVcUowY21MekQxa3pHemhlaUJhZzBZUkF6VmRsZnhOMjgwTmdIYUEifQ.eyJfc2QiOiBbIk5ZQ29TUktFWXdYZHBlNXlkdUpYQ3h4aHluRVU4ei1iNFR5TmlhcDc3VVkiLCAiU1k4bjJCYmtYOWxyWTNleEhsU3dQUkZYb0QwOUdGOGE5Q1BPLUc4ajIwOCIsICJUUHNHTlBZQTQ2d21CeGZ2MnpuT0poZmRvTjVZMUdrZXpicGFHWkNUMWFjIiwgIlprU0p4eGVHbHVJZFlCYjdDcWtaYkpWbTB3MlY1VXJSZU5UekFRQ1lCanciLCAibDlxSUo5SlRRd0xHN09MRUlDVEZCVnhtQXJ3OFBqeTY1ZEQ2bXRRVkc1YyIsICJvMVNBc0ozM1lNaW9POXBYNVZlQU0xbHh1SEY2aFpXMmtHZGtLS0JuVmxvIiwgInFxdmNxbmN6QU1nWXg3RXlrSTZ3d3RzcHl2eXZLNzkwZ2U3TUJiUS1OdXMiXSwgImlzcyI6ICJodHRwczovL2V4YW1wbGUuY29tL2lzc3VlciIsICJpYXQiOiAxNTE2MjM5MDIyLCAiZXhwIjogMTUxNjI0NzAyMiwgIl9zZF9hbGciOiAic2hhLTI1NiIsICJjbmYiOiB7Imp3ayI6IHsia3R5IjogIlJTQSIsICJuIjogInBtNGJPSEJnLW9ZaEF5UFd6UjU2QVdYM3JVSVhwMTFfSUNEa0dnUzZXM1pXTHRzLWh6d0kzeDY1NjU5a2c0aFZvOWRiR29DSkUzWkdGX2VhZXRFMzBVaEJVRWdwR3dyRHJRaUo5enFwcm1jRmZyM3F2dmtHanR0aDhaZ2wxZU0yYkpjT3dFN1BDQkhXVEtXWXMxNTJSN2c2SmcyT1ZwaC1hOHJxLXE3OU1oS0c1UW9XX21UejEwUVRfNkg0YzdQaldHMWZqaDhocFdObmJQX3B2NmQxelN3WmZjNWZsNnlWUkwwRFYwVjNsR0hLZTJXcWZfZU5HakJyQkxWa2xEVGs4LXN0WF9NV0xjUi1FR21YQU92MFVCV2l0U19kWEpLSnUtdlhKeXcxNG5IU0d1eFRJSzJoeDFwdHRNZnQ5Q3N2cWltWEtlRFRVMTRxUUwxZUU3aWhjdyIsICJlIjogIkFRQUIifX19.xqgKrDO6dK_oBL3fiqdcq_elaIGxM6Z-RyuysglGyddR1O1IiE3mIk8kCpoqcRLR88opkVWN2392K_XYfAuAmeT9kJVisD8ZcgNcv-MQlWW9s8WaViXxBRe7EZWkWRQcQVR6jf95XZ5H2-_KA54POq3L42xjk0y5vDr8yc08Reak6vvJVvjXpp-Wk6uxsdEEAKFspt_EYIvISFJhfTuQqyhCjnaW13X312MSQBPwjbHn74ylUqVLljDvqcemxeqjh42KWJq4C3RqNJ7anA2i3FU1kB4-KNZWsijY7-op49iL7BrnIBxdlAMrbHEkoGTbFWdl7Ki17GHtDxxa1jaxQg~WyIweEd6bjNNaXFzY3RaSV9PcERsQWJRIiwgImFkZHJlc3MiLCB7InN0cmVldF9hZGRyZXNzIjogIjEyMyBNYWluIFN0IiwgImxvY2FsaXR5IjogIkFueXRvd24iLCAicmVnaW9uIjogIkFueXN0YXRlIiwgImNvdW50cnkiOiAiVVMifV0~WyJxUVdtakpsMXMxUjRscWhFTkxScnJ3IiwgImZhbWlseV9uYW1lIiwgIkRvZSJd~WyIzanFjYjY3ejl3a3MwOHp3aUs3RXlRIiwgImdpdmVuX25hbWUiLCAiSm9obiJd~eyJhbGciOiAiUlMyNTYiLCAia2lkIjogIkxkeVRYd0F5ZnJpcjRfVjZORzFSYzEwVThKZExZVHJFQktKaF9oNWlfclUifQ.eyJub25jZSI6ICJYWk9VY28xdV9nRVBrbnhTNzhzV1dnIiwgImF1ZCI6ICJodHRwczovL2V4YW1wbGUuY29tL3ZlcmlmaWVyIiwgImlhdCI6IDE2NzAzNjY0MzB9.aJ1HRh21dXP2_pMCHmkqNKaDN25aKuJGiTu2XKOegQ2xjZ_OzLu5_6gNy0qMhJIXb28-uQc7pMpWMZqkQLV3d7HGBiqiA71RsYYUCF-4VYrFNFb63HQxOHTcu-nXALwbJgBSbQXKjdpjOj6NrIwItQNh7_EQ3vrf7E0_exmkNoZxy0QCGqApAwUtiLtwQDExM9UYgms_ayPDZ3TJVcWHd9UtQTJugF4fTCJnq6TQLKuUioK-kLAbDuRamnOpQ5d6Rbz3NHWdVp3nHM6bgh_3Y0zBFdg6VsmAXvnoIN9b2jzSemNNAniAsalmd4X5pwzyK1TUHFJm-ib_kFf2G7Iapw";
        
        private string _issuerJwk = "{\"kty\": \"EC\",\"d\": \"Ur2bNKuBPOrAaxsRnbSH6hIhmNTxSGXshDSUD1a1y7g\",\"crv\": \"P-256\",\"x\":\"b28d4MwZMjw8-00CG4xfnn9SLMVMM19SlqZpVb_uNtQ\",\"y\":\"Xv5zWwuoaTgdS6hV43yI6gBwTnjukmFQQnJ_kCxzqk8\",}";
        
        private Verifier _verifier;

        [SetUp]
        public void Setup()
        {
            _verifier = new Verifier();
        }
        
        [Test]
        public void CanVerifyPresentation()
        {
            Assert.True(_verifier.VerifyPresentation(_expectedPresentation, _issuerJwk));
        }
    }
}

