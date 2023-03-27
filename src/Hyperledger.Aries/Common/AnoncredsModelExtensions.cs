using anoncreds_rs_dotnet.Anoncreds;
using anoncreds_rs_dotnet;
using anoncreds_rs_dotnet.Models;
using Hyperledger.Aries.Ledger.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Flurl.Util;

namespace Hyperledger.Aries.Common
{
    public static class AnoncredsModelExtensions
    {
        public enum AnoncredsModel
        {
            Schema,
            CredDef,
            Cred,
            CredOffer,
            CredReq,
            RevRegDef,
        }

        /** Todo : fix workaround when new indy_vdr version is released. 
             * Workaround for compatibility with indy_vdr cause it uses old shared-rs Schema models, but latest anoncreds-rs are different.
            Old shared-rs model
            {
              "ver":"{}",
              "name":"{}"
              "version":"{}"
              "attrNames":[],
              "id":"{}"
            }

            New anoncreds-rs model
            {
              "name":"{}"
              "version":"{}"
              "attrNames":[],
              "issuerId":"{}"
            }
            **/

        public static string ToAnoncredsJson(this string modelJson, AnoncredsModel model, string id = null, int seqNo = 0)
        {
            string resultJson = "";
            switch (model)
            {
                case AnoncredsModel.Schema:
                    Schema schema = JsonConvert.DeserializeObject<Schema>(modelJson);

                    resultJson = "{" +
                    $"\"name\":\"{schema.Name}\"," +
                    $"\"version\":\"{schema.Version}\"," +
                    $"\"attrNames\": {JsonConvert.SerializeObject(schema.AttrNames)}, " +
                    $"\"issuerId\":\"{JObject.Parse(modelJson)["id"]}\"" +
                    "}";
                    break;
                case AnoncredsModel.CredDef:
                    CredentialDefinition credDef = JsonConvert.DeserializeObject<CredentialDefinition>(modelJson);

                    try
                    {
                        JObject jObj = JObject.Parse(modelJson);
                        credDef.Value.Primary.R = new List<KeyProofAttributeValue>();
                        foreach (JToken ele in jObj["value"]["primary"]["r"])
                        {
                            string[] attrFields = ele.ToString().Split(':');
                            KeyProofAttributeValue attribute = new KeyProofAttributeValue(JsonConvert.DeserializeObject<string>(attrFields[0]), JsonConvert.DeserializeObject<string>(attrFields[1]));
                            credDef.Value.Primary.R.Add(attribute);
                        }
                    }
                    catch (Exception e)
                    {
                        throw new ArgumentException("Could not find field r.", e);
                    }

                    string primaryR = "{";
                    foreach (var ele in credDef.Value.Primary.R)
                    {
                        primaryR += $"\"{ele.Name}\" : \"{ele.Value}\",";
                    }
                    primaryR = primaryR.AsSpan(0, primaryR.Length - 1).ToString() + "}";
                    resultJson = "{" +
                        $"\"schemaId\":\"{credDef.SchemaId}\"," +
                        $"\"type\":\"{credDef.SignatureType}\"," +
                        $"\"tag\": \"{credDef.Tag}\", " +
                        $"\"issuerId\":\"{JObject.Parse(modelJson)["id"]}\"," +
                        $"\"value\":{{\"primary\": {{\"n\": \"{credDef.Value.Primary.N}\", \"s\": \"{credDef.Value.Primary.S}\", \"rctxt\": \"{credDef.Value.Primary.Rctxt}\", \"z\": \"{credDef.Value.Primary.Z}\", \"r\" : {primaryR} }}, \"revocation\" : {JsonConvert.SerializeObject(credDef.Value.Revocation)} }}" +
                    "}";

                    break;
            }

            return resultJson;

        }

        public static string ToSharedRsJson(this string modelJson, AnoncredsModel model, string id = null, string seqNo = null)
        {
            string resultJson = "";
            switch (model)
                {
                case AnoncredsModel.Schema:
                    Schema schema = JsonConvert.DeserializeObject<Schema>(modelJson);

                    string tempAttributeNames = "";
                    foreach (var ele in schema.AttrNames)
                    {
                        tempAttributeNames += $"{JsonConvert.SerializeObject(ele)},";
                    }
                    tempAttributeNames = tempAttributeNames.AsSpan(0, tempAttributeNames.Length - 1).ToString();

                    resultJson =
                        "{" +
                            $"\"ver\":\"{schema.Version}\"," +
                            $"\"name\":\"{schema.Name}\"," +
                            $"\"version\":\"{schema.Version}\"," +
                            $"\"attrNames\": [{tempAttributeNames}], " +
                            $"\"id\":\"{schema.IssuerId}\"" +
                        "}";
                    break;
                case AnoncredsModel.CredDef:
                    CredentialDefinition credDef = JsonConvert.DeserializeObject<CredentialDefinition>(modelJson);

                    try
                    {
                        JObject jObj = JObject.Parse(modelJson);
                        credDef.Value.Primary.R = new List<KeyProofAttributeValue>();
                        foreach (JToken ele in jObj["value"]["primary"]["r"])
                        {
                            string[] attrFields = ele.ToString().Split(':');
                            KeyProofAttributeValue attribute = new KeyProofAttributeValue(JsonConvert.DeserializeObject<string>(attrFields[0]), JsonConvert.DeserializeObject<string>(attrFields[1]));
                            credDef.Value.Primary.R.Add(attribute);
                        }
                    }
                    catch (Exception e)
                    {
                        throw new ArgumentException("Could not find field r.", e);
                    }

                    string schemaId, credDefId;
                    if (seqNo != null)
                    {
                        schemaId = seqNo.ToString();
                    }
                    else schemaId = credDef.SchemaId;

                    if (id != null)
                    {
                        credDefId = id;
                    }
                    else credDefId = JObject.Parse(modelJson)["issuerId"].ToString();

                    string primaryR = "{";
                    foreach (var ele in credDef.Value.Primary.R)
                    {
                        primaryR += $"\"{ele.Name}\" : \"{ele.Value}\",";
                    }
                    primaryR = primaryR.AsSpan(0, primaryR.Length - 1).ToString() + "}";
                    resultJson = "{" +
                        $"\"ver\":\"1.0\"," +
                        $"\"schemaId\":\"{schemaId}\"," +
                        $"\"type\":\"{credDef.SignatureType}\"," +
                        $"\"tag\": \"{credDef.Tag}\", " +
                        $"\"id\":\"{credDefId}\"," +
                        //$"\"value\":{JsonConvert.SerializeObject(credDef.Value)}" +
                        $"\"value\":{{\"primary\": {{\"n\": \"{credDef.Value.Primary.N}\", \"s\": \"{credDef.Value.Primary.S}\", \"rctxt\": \"{credDef.Value.Primary.Rctxt}\", \"z\": \"{credDef.Value.Primary.Z}\", \"r\" : {primaryR} }}, \"revocation\" : {JsonConvert.SerializeObject(credDef.Value.Revocation)} }}" +
                    "}";
                    break;
            }

            return resultJson;

        }
    }
}
