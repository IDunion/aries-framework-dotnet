using System.Collections.Generic;
using SdJwt.Models;

namespace SdJwt.Abstractions
{
    public interface IIssuer
    {
        public string Issue(List<Claim> claims, string issuerJwk);
    }
}
