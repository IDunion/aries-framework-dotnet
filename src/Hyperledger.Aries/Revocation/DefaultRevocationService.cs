using anoncreds_rs_dotnet.Anoncreds;
using anoncreds_rs_dotnet.Models;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Revocation.Abstractions;
using Hyperledger.Aries.Revocation.Models;
using Hyperledger.Aries.Revocation.Utils;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static Hyperledger.Aries.Revocation.Models.RevRegDefinitionState;

namespace Hyperledger.Aries.Revocation
{
    public class DefaultRevocationService :IRevocationService
    {
        private List<BaseAnonCredsRegistrar> registrars;
        private Profile _profile;
    

        public DefaultRevocationService(Profile profile)
        {
            _profile = profile;
        }

        public Profile Profile
        {
            // Accessor for the profile instance

            get
            {
                if (!(_profile is AskarAnoncredsProfile))
                {
                    throw new Exception("AnonCreds interface requires AskarAnoncreds profile");
                }

                return _profile;
            }
        }
        public async Task<RevRegDefResult> CreateAndRegisterRevocationRegistryDefinitionAsync(Profile profile, string originDid, CredentialDefinition credDefObject, string credDefId, string tag, RegistryType revRegType, long maxCredNumber, string tailsDirPath)
        {
            var req = await RevocationApi.CreateRevocationRegistryDefinitionAsync(originDid, credDefObject, credDefId, tag, revRegType, maxCredNumber, tailsDirPath);
            RevocationRegistryDefinition revRegDef = req.Item1;
            RevocationRegistryDefinitionPrivate revRegDefPrivate = req.Item2;
            RevRegDefResult result = await RegisterRevocationRegistryDefinition(profile, revRegDef);
            return result;
        }

        public async Task<RevRegDefResult> RegisterRevocationRegistryDefinition(Profile profile, RevocationRegistryDefinition revRegDef)
        {
            RevRegDefResult result;
            var registrar = await _registrar_for_identifier(revRegDef.IssuerId);
            result = await registrar.RegisterRevocationRegistryDefinition(profile, revRegDef);
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
