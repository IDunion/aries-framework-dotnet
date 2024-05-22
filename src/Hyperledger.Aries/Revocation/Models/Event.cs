using System;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.Revocation.Models
{
    public class Event
    {
        // A simple event object.

        private string _topic;
        private object _payload;

        public Event(string topic, object payload = null)
        {
            // Create a new event.
            _topic = topic;
            _payload = payload;
        }

        public string Topic
        {
            // Return this event's topic.
            get { return _topic; }
        }

        public object Payload
        {
            // Return this event's payload.
            get { return _payload; }
        }
    }
}
