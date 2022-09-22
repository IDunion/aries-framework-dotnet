using Hyperledger.Aries.Common;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.IssueCredential;
using Hyperledger.Aries.Storage.Models;
using Hyperledger.Indy.AnonCredsApi;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Utils
{
    /// <summary>
    /// Credential utilities
    /// </summary>
    public static class CredentialUtils
    {
        /// <summary>
        /// Formats the credential values into three string lists usable with the <see cref="indy_shared_rs_dotnet.IndyCredx.CredentialApi"/> API
        /// </summary>
        /// <returns>The credential values as three string lists. First is attribute names, second is attribute raw values , third is attribute encoded values.</returns>
        /// <param name="credentialAttributes">The credential attributes.</param>
        public static (List<string>, List<string>, List<string>) FormatCredentialValuesForIndySharedRs(IEnumerable<CredentialPreviewAttribute> credentialAttributes)
        {
            if (credentialAttributes == null)
            {
                return (null, null, null);
            }

            List<string> resultAttrNames = new();
            List<string> resultAttrNamesRaw = new();

            foreach (CredentialPreviewAttribute item in credentialAttributes)
            {
                switch (item.MimeType)
                {
                    case CredentialMimeTypes.TextMimeType:
                    case CredentialMimeTypes.ApplicationJsonMimeType:
                    case CredentialMimeTypes.ImagePngMimeType:
                        resultAttrNames.Add(item.Name);
                        resultAttrNamesRaw.Add((string)item.Value);
                        break;
                    default:
                        throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, $"{item.Name} mime type of {item.MimeType} not supported");
                }
            }
            List<string> resultAttrNamesEnc = indy_shared_rs_dotnet.IndyCredx.CredentialApi.EncodeCredentialAttributesAsync(resultAttrNamesRaw).GetAwaiter().GetResult();
            return (resultAttrNames, resultAttrNamesRaw, resultAttrNamesEnc);
        }

        /// <summary>
        /// Formats the credential values into a JSON usable with the <see cref="AnonCreds"/> API
        /// </summary>
        /// <returns>The credential values.</returns>
        /// <param name="credentialAttributes">The credential attributes.</param>
        public static string FormatCredentialValues(IEnumerable<CredentialPreviewAttribute> credentialAttributes)
        {
            if (credentialAttributes == null)
            {
                return null;
            }

            Dictionary<string, Dictionary<string, string>> result = new();
            foreach (CredentialPreviewAttribute item in credentialAttributes)
            {
                switch (item.MimeType)
                {
                    case CredentialMimeTypes.TextMimeType:
                    case CredentialMimeTypes.ApplicationJsonMimeType:
                    case CredentialMimeTypes.ImagePngMimeType:
                        result.Add(item.Name, FormatStringCredentialAttribute(item));
                        break;
                    default:
                        throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, $"{item.Name} mime type of {item.MimeType} not supported");
                }
            }
            return result.ToJson();
        }

        private static readonly SHA256 sha256 = SHA256.Create();
        private static Dictionary<string, string> FormatStringCredentialAttribute(CredentialPreviewAttribute attribute)
        {
            return new Dictionary<string, string>()
            {
                {"raw", (string) attribute.Value},
                {"encoded", GetEncoded((string) attribute.Value)}
            };
        }

        internal static string GetEncoded(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                value = string.Empty;
            }

            if (int.TryParse(value, out int result))
            {
                return result.ToString();
            }

            byte[] data = new byte[] { 0 }
                .Concat(sha256.ComputeHash(value.GetUTF8Bytes()))
                .ToArray();

            Array.Reverse(data);
            return new BigInteger(value: data).ToString();

            /*
                netstandard2.1 includes the ctor below,
                which allows to specify expected sign
                and endianess

            return new BigInteger(
                value: data,
                isUnsigned: true,
                isBigEndian: true).ToString();
            */
        }

        /// <summary>
        /// Checks if the value is encoded correctly
        /// </summary>
        /// <param name="raw"></param>
        /// <param name="encoded"></param>
        /// <returns></returns>
        public static bool CheckValidEncoding(string raw, string encoded)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                raw = string.Empty;
            }

            return int.TryParse(raw, out int _) ? string.CompareOrdinal(raw, encoded) == 0 : string.CompareOrdinal(encoded, GetEncoded(raw)) == 0;
        }

        /// <summary>
        /// Validates if the credential preview attribute is valid.
        /// </summary>
        /// <param name="attribute">Credential preview attribute.</param>
        public static void ValidateCredentialPreviewAttribute(CredentialPreviewAttribute attribute)
        {
            switch (attribute.MimeType)
            {
                case null:
                case CredentialMimeTypes.TextMimeType:
                case CredentialMimeTypes.ApplicationJsonMimeType:
                case CredentialMimeTypes.ImagePngMimeType:
                    break;
                default:
                    throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, $"{attribute.Name} mime type of {attribute.MimeType} not supported");
            }
        }

        /// <summary>
        /// Validates if the credential preview attributes are valid.
        /// </summary>
        /// <param name="attributes">Credential preview attributes.</param>
        public static void ValidateCredentialPreviewAttributes(IEnumerable<CredentialPreviewAttribute> attributes)
        {
            List<string> validationErrors = new();

            foreach (CredentialPreviewAttribute attribute in attributes)
            {
                try
                {
                    ValidateCredentialPreviewAttribute(attribute);
                }
                catch (AriesFrameworkException e)
                {
                    validationErrors.Add(e.Message);
                }
            }

            if (validationErrors.Any())
            {
                throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, validationErrors.ToArray());
            }
        }

        /// <summary>
        /// Casts an attribute value object to its respective type.
        /// </summary>
        /// <param name="attributeValue">Attribute value object.</param>
        /// <param name="mimeType">Mime type to cast the attribute value to.</param>
        /// <returns></returns>
        public static object CastAttribute(object attributeValue, string mimeType)
        {
            return mimeType switch
            {
                CredentialMimeTypes.TextMimeType or CredentialMimeTypes.ApplicationJsonMimeType or CredentialMimeTypes.ImagePngMimeType => (string)attributeValue,
                _ => throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, $"Mime type of {mimeType} not supported"),
            };
        }

        /// <summary>
        /// Casts an attribute value object to its respective type.
        /// </summary>
        /// <param name="attributeValue">Attribute value object.</param>
        /// <param name="mimeType">Mime type to cast the attribute value to.</param>
        /// <returns></returns>
        public static object CastAttribute(JToken attributeValue, string mimeType)
        {
            return mimeType switch
            {
                null => attributeValue.Value<string>(),
                CredentialMimeTypes.TextMimeType or CredentialMimeTypes.ApplicationJsonMimeType or CredentialMimeTypes.ImagePngMimeType => attributeValue.Value<string>(),
                _ => throw new AriesFrameworkException(ErrorCode.InvalidParameterFormat, $"Mime type of {mimeType} not supported"),
            };
        }

        /// <summary>
        /// Gets the attributes.
        /// </summary>
        /// <param name="jsonAttributeValues">The json attribute values.</param>
        /// <returns></returns>
        public static Dictionary<string, string> GetAttributes(string jsonAttributeValues)
        {
            if (string.IsNullOrEmpty(jsonAttributeValues))
            {
                return new Dictionary<string, string>();
            }

            JObject attributes = JObject.Parse(jsonAttributeValues);

            Dictionary<string, string> result = new();
            foreach (KeyValuePair<string, JToken> attribute in attributes)
            {
                result.Add(attribute.Key, attribute.Value["raw"].ToString());
            }

            return result;
        }
    }
}
