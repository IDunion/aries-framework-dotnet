using Hyperledger.Aries.Agents;
using Newtonsoft.Json;
using System;

namespace Hyperledger.Aries.Features.Handshakes.Connection.Models
{
    /// <summary>
    /// Connection acknowledge message
    /// </summary>
    public class ConnectionAcknowledgeMessage : AgentMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionAcknowledgeMessage" /> class.
        /// </summary>
        public ConnectionAcknowledgeMessage()
        {
            Id = Guid.NewGuid().ToString();
            Type = UseMessageTypesHttps ? MessageTypesHttps.ConnectionAcknowledgement : MessageTypes.ConnectionAcknowledgement;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionAcknowledgeMessage" /> class.
        /// </summary>
        public ConnectionAcknowledgeMessage(bool useMessageTypesHttps) : base(useMessageTypesHttps)
        {
            Id = Guid.NewGuid().ToString();
            Type = UseMessageTypesHttps ? MessageTypesHttps.ConnectionAcknowledgement : MessageTypes.ConnectionAcknowledgement;
        }

        /// <summary>
        /// Gets or sets the acknowledgement status.
        /// </summary>
        [JsonProperty("status")]
        public string Status { get; set; }
    }
}
