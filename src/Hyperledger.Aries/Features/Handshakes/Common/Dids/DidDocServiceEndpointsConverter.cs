using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Hyperledger.Aries.Features.Handshakes.Common.Dids
{
    internal class DidDocServiceEndpointsConverter : JsonConverter
    {
        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteRawValue(JsonConvert.SerializeObject(value));
        }

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            JArray items = JArray.Load(reader);

            IList<IDidDocServiceEndpoint> serviceEndpoints = new List<IDidDocServiceEndpoint>();

            if (items == null)
            {
                return serviceEndpoints;
            }

            foreach (JToken item in items)
            {
                IDidDocServiceEndpoint serviceEndpoint = item["type"].ToObject<string>() switch
                {
                    DidDocServiceEndpointTypes.IndyAgent => new IndyAgentDidDocService(),
                    _ => throw new TypeLoadException("Unsupported serialization type."),
                };
                serializer.Populate(item.CreateReader(), serviceEndpoint);
                serviceEndpoints.Add(serviceEndpoint);
            }
            return serviceEndpoints;
        }

        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return true;
        }
    }
}
