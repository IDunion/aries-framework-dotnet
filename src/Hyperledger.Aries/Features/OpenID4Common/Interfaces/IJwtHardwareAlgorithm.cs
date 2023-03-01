using JWT.Algorithms;
using Microsoft.IdentityModel.Tokens;

namespace Hyperledger.Aries.Features.OpenID4Common.Interfaces
{
    public interface IJwtHardwareAlgorithm : IJwtAlgorithm
    {
        JsonWebKey Init(string alias);
        JsonWebKey CreateJsonWebKey();
        void SetAlias(string alias);
    }
}
