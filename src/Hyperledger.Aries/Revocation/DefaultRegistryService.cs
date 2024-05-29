using anoncreds_rs_dotnet.Models;
using Hyperledger.Aries.Revocation.Abstractions;
using Hyperledger.Aries.Revocation.Models;
using Hyperledger.Aries.Revocation.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Revocation
{
    public class DefaultRegistryService : IRegistryService
    {
        private List<BaseAnonCredsRegistrar> registrars;
        public async Task<RevRegDefResult> RegisterRevocationRegistryDefinition(RevocationRegistryDefinition revRegDef)
        {
            RevRegDefResult result;
            var registrar = await _registrar_for_identifier(revRegDef.IssuerId);
            result = await registrar.RegisterRevocationRegistryDefinition(revRegDef);
            return result;
        }

        public async Task<BaseAnonCredsRegistrar> _registrar_for_identifier(string IssuerId)
        {
            List<BaseAnonCredsRegistrar> matchingRegistrars = new List<BaseAnonCredsRegistrar>();
            foreach (var registrar in registrars)
            {
                if (await registrar.SupportsAsync(IssuerId))
                {
                    matchingRegistrars.Add(registrar);
                }
            }

            if (matchingRegistrars.Count == 0)
            {
                throw new AnonCredsRegistrationError($"No registrar available for identifier {IssuerId}");
            }

            if (matchingRegistrars.Count > 1)
            {
                throw new AnonCredsRegistrationError($"More than one registrar found for identifier {IssuerId}");
            }

            return matchingRegistrars[0];
        }
    }
}
