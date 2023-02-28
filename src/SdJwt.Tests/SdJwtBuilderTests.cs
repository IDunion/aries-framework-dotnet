using System.Security.Cryptography;
using JWT.Algorithms;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using SdJwt.Models;

namespace SdJwt.Tests
{
    public class SdJwtBuilderTests
{
    [Test]
    public void CanAddClaims()
    {
        var sdJwtDoc = new SdJwtBuilder();
        
        Assert.That(sdJwtDoc.ToString().Equals("{}"));

        var claim1 = new
        {
            street = "Schulstraße 12",
            city = "Frankfurt"
        };
        var claim2 = "Max";
        
        sdJwtDoc.AddClaim(new Claim("address", claim1));
        sdJwtDoc.AddClaim(new Claim("name", claim2, true));
        
        Assert.That(sdJwtDoc.ToString().Equals("{}"));
        
        sdJwtDoc.Build();
        
        Assert.That(!sdJwtDoc.ToString().Equals("{}"));
    }

    [Test]
    public void CanBuildSdJwt()
    {
        var sdJwtBuilder = new SdJwtBuilder();
        
        var claim1 = new
        {
            street = "Schulstraße 12",
            city = "Frankfurt"
        };
        
        sdJwtBuilder.AddClaim(new Claim("address", claim1, true));

        using ECDsa ecDsa = ECDsa.Create();
        var alg = new ES256Algorithm(ecDsa, ecDsa);
        
        sdJwtBuilder.AddAlgorithm(alg);

        var jwt = sdJwtBuilder.Build();
        
        Assert.That(!string.IsNullOrEmpty(jwt));
        Assert.AreEqual(3, jwt.Split('~').Length);
    }
    
    [Test]
    public void CanBuildSdJwtWithHolderBinding()
    {
        var sdJwtBuilder = new SdJwtBuilder();
        
        var claim1 = new
        {
            street = "Schulstraße 12",
            city = "Frankfurt"
        };
        
        sdJwtBuilder.AddClaim(new Claim("address", claim1, true));
        
        using ECDsa issuerKey = ECDsa.Create();
        var alg = new ES256Algorithm(issuerKey, issuerKey);
        
        using ECDsa holderKey = ECDsa.Create();
        ECDsaSecurityKey key = new ECDsaSecurityKey(holderKey);
        
        sdJwtBuilder.AddAlgorithm(alg);
        
        sdJwtBuilder.AddHolderBinding(JsonWebKeyConverter.ConvertFromSecurityKey(key));

        var sdJwt = sdJwtBuilder.Build();
        
        Assert.That(!string.IsNullOrEmpty(sdJwt));
        Assert.AreEqual(3, sdJwt.Split('~').Length);
    }
}
}

