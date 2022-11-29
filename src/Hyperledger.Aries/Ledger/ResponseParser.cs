using Flurl.Util;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Ledger.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace Hyperledger.Aries.Ledger
{
    internal static class ResponseParser
    {
        /// <summary>
        /// Parse a GET_SCHEMA response to get Schema in the format compatible with Anoncreds API.
        /// </summary>
        /// <returns>
        /// Schema Id and Schema json.
        /// {
        ///     id: identifier of schema
        ///     attrNames: array of attribute name strings
        ///     name: Schema's name string
        ///     version: Schema's version string
        ///     ver: Version of the Schema json
        /// }</returns>
        /// <param name="getSchemaResponse">response of GET_SCHEMA request.</param>
        internal static AriesResponse ParseGetSchemaResponse(string getSchemaResponse)
        {
            JToken schema = JObject.Parse(getSchemaResponse)["result"]!;
            string schemaId = $"{schema["dest"]}:2:{schema["data"]!["name"]}:{schema["data"]!["version"]}";
            var objectJson = new
            {
                ver = "1.0",
                id = schemaId,
                attrNames = schema["data"]!["attr_names"]!.Select(x => x.ToString()).ToArray(),
                name = schema["data"]!["name"]!,
                version = schema["data"]!["version"]!,
                seqNo = schema["seqNo"]!
            };
            return new AriesResponse(schemaId, objectJson.ToJson());
        }

        /// <summary>
        /// Parse a GET_CRED_DEF response to get Credential Definition in the format compatible with Anoncreds API.
        /// Credential Definition Id and Credential Definition json.
        /// </summary>
        /// <returns>
        /// Credential Definition Id and Credential Definition json.
        /// {
        ///     id: string - identifier of credential definition
        ///     schemaId: string - identifier of stored in ledger schema
        ///     type: string - type of the credential definition. CL is the only supported type now.
        ///     tag: string - allows to distinct between credential definitions for the same issuer and schema
        ///     value: Dictionary with Credential Definition's data: {
        ///         primary: primary credential public key,
        ///         revocation: revocation credential public key [optional]
        ///     },
        ///     ver: Version of the Credential Definition json
        /// }
        /// </returns>
        internal static AriesResponse ParseGetCredDefResponse(string credDefId, string getCredDefResponse)
        {
            CredDefId id = new(credDefId);
            JToken credDef = JObject.Parse(getCredDefResponse)["result"]!;
            var objectJson = new
            {
                ver = "1.0",
                id = id.ToString(),
                schemaId = credDef!["ref"], //here it is the schemaSequenceId
                type = credDef!["signature_type"]!,
                tag = credDef["tag"]!,
                value = credDef["data"]!
            };
            return new AriesResponse(credDefId, objectJson.ToJson());
        }

        /// <summary>
        /// Parse a GET_REVOC_REG_DEF response to get Revocation Registry Definition in the format compatible with
        /// Anoncreds API.
        /// </summary>
        /// <param name="registryDefinitionId"></param>
        /// <param name="getRegistryDefinitionResponse"></param>
        /// <returns>
        /// /// Revocation Registry Definition Id and Revocation Registry Definition json.
        /// {
        ///     "id": string - ID of the Revocation Registry,
        ///     "revocDefType": string - Revocation Registry type (only CL_ACCUM is supported for now),
        ///     "tag": string - Unique descriptive ID of the Registry,
        ///     "credDefId": string - ID of the corresponding CredentialDefinition,
        ///     "value": Registry-specific data {
        ///         "issuanceType": string - Type of Issuance(ISSUANCE_BY_DEFAULT or ISSUANCE_ON_DEMAND),
        ///         "maxCredNum": number - Maximum number of credentials the Registry can serve.
        ///         "tailsHash": string - Hash of tails.
        ///         "tailsLocation": string - Location of tails file.
        ///         "publicKeys": Registry's public key.
        ///     },
        ///     "ver": string - version of revocation registry definition json.
        /// }
        /// </returns>
        internal static AriesResponse ParseRegistryDefinitionResponse(string registryDefinitionId, string getRegistryDefinitionResponse)
        {
            JToken revRegDef = JObject.Parse(getRegistryDefinitionResponse)["result"]!["data"];
            var objectJson = new
            {
                ver = "1.0",
                id = revRegDef["id"]!,
                revocDefType = revRegDef!["revocDefType"],
                tag = revRegDef["tag"],
                credDefId = revRegDef["credDefId"],
                value = revRegDef["value"]
            };

            return new AriesResponse(registryDefinitionId, objectJson.ToJson());
        }

        /// <summary>
        /// Parse revocation registry response result in the format compatible with Anoncreds API.
        /// </summary>
        /// <param name="revocRegResponse">Ledger response from revocation registry lookup</param>
        /// <returns><see cref="ParseRegistryResponseResult"/></returns>
        internal static AriesRegistryResponse ParseRevocRegResponse(string revocRegResponse)
        {
            JsonSerializerSettings settings = new()
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            JToken jobj = JObject.Parse(revocRegResponse)["result"]!;
            string revocRegDefId = jobj["revocRegDefId"]!.ToString();
            object accum = jobj["data"]!["value"]?["accum_to"]?["value"]?.ToKeyValuePairs().First().Value;
            object prevAccum = jobj["data"]!["value"]?["accum_from"]?["value"]?.ToKeyValuePairs().First().Value;
            List<JToken> issued = jobj["data"]!["value"]?["issued"]?.ToList();
            issued.ForEach(x => x.ToObject<uint>());
            List<JToken> revoked = jobj["data"]!["value"]?["revoked"]?.ToList();
            revoked.ForEach(x => x.ToObject<uint>());

            string value = JsonConvert.SerializeObject(new
            {
                accum,
                issued,
                revoked,
                prev_accum = prevAccum
            }, settings);

            string data = JsonConvert.SerializeObject(new
            {
                ver = "1.0",
                value = JsonConvert.DeserializeObject<Dictionary<string, object>>(value),
            }, settings);

            ulong timestamp = (ulong)jobj["data"]!["value"]?["accum_to"]?["txnTime"]!;
            return new AriesRegistryResponse(revocRegDefId, data, timestamp);
        }
    }
}
