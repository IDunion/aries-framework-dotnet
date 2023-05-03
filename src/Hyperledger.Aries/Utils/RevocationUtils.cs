using anoncreds_rs_dotnet.Models;
using System.Collections.Generic;
using System.Linq;
using Hyperledger.Aries.Extensions;
using Newtonsoft.Json;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Features.PresentProof;

namespace Hyperledger.Aries.Utils
{
    public static class RevocationUtils
    {
        public static RevocationStatusList ConvertDeltaToRevocationStatusList(
            string revRegDefId,
            string revRegDefJson,
            string deltaJson, 
            long timestamp)
        {
            RevocationRegistryDefinition revocationRegistryDefinition = 
                JsonConvert.DeserializeObject<RevocationRegistryDefinition>(revRegDefJson);

            RevocationRegistryDelta delta =
                JsonConvert.DeserializeObject<RevocationRegistryDelta>(deltaJson);

            bool isIssuanceByDefault = revocationRegistryDefinition.Value.IssuanceType == IssuerType.ISSUANCE_BY_DEFAULT.ToString();

            // 0 means unrevoked, 1 means revoked
            byte defaultState = isIssuanceByDefault? (byte)0 : (byte)1;

            int maxCredNumber = revocationRegistryDefinition.Value.MaxCredNum;

            // Fill with default value
            List<byte> revocationList = Enumerable.Repeat(element: defaultState, count: maxCredNumber).ToList();

            IEnumerable<int> issuedList =
                delta.Value.Issued == null ? new List<int>() : delta.Value.Issued.Select(x => (int)x);
            // Set all `issuer` indexes to 0 (not revoked)
            foreach (int issued in issuedList)
            {
                revocationList[issued] = (byte)0;
            }
            
            IEnumerable<int> revokedList = 
                delta.Value.Revoked == null ? new List<int>() : delta.Value.Revoked.Select(x => (int)x);
            // Set all `revoked` indexes to 1 (revoked)
            foreach (int revoked in revokedList)
            {
                revocationList[revoked] = (byte)1;
            }

            RevocationStatusList revStatusList = new()
            {
                IssuerId = revocationRegistryDefinition.IssuerId,
                RevocationRegistryDefinitionId = revRegDefId,//revocationRegistryDefinition.IssuerId,
                RevocationList = revocationList,
                Timestamp = timestamp,
                RevocationRegistry = delta.Value.Accum
            };

            return revStatusList;
        }

        public static string ConvertDeltaToRevocationStatusListJson(
            string revRegDefId,
            string revRegDefJson,
            string deltaJson, 
            long timestamp)
        {
            return ConvertDeltaToRevocationStatusList(
                revRegDefId,
                revRegDefJson, 
                deltaJson, 
                timestamp).ToJson(new JsonSerializerSettings{DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore});
        }
    }
}
