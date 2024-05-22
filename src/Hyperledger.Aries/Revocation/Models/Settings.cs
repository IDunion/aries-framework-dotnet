using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.Revocation.Models
{
    public class Settings : BaseSettings, IDictionary<string, object>
    {
        // Mutable settings implementation.

        private Dictionary<string, object> _values = new Dictionary<string, object>();

        public Settings(Dictionary<string, object> values = null)
        {
            // Initialize a Settings object.

            if (values != null)
            {
                foreach (var item in values)
                {
                    _values.Add(item.Key, item.Value);
                }
            }
        }

        public override object this[string key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override ICollection<string> Keys => throw new NotImplementedException();

        public override ICollection<object> Values => throw new NotImplementedException();

        public override bool IsReadOnly => throw new NotImplementedException();

        public override int Count => throw new NotImplementedException();

        public override IEnumerable<KeyValuePair<string, object>> Items => throw new NotImplementedException();

        public override void Add(KeyValuePair<string, object> item)
        {
            throw new NotImplementedException();
        }

        public override void Add(string key, object value)
        {
            throw new NotImplementedException();
        }

        public override void Clear()
        {
            throw new NotImplementedException();
        }

        public override bool Contains(KeyValuePair<string, object> item)
        {
            throw new NotImplementedException();
        }

        public override bool ContainsKey(string key)
        {
            throw new NotImplementedException();
        }

        public override BaseSettings Copy()
        {
            throw new NotImplementedException();
        }

        public override void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public override BaseSettings Extend(IDictionary<string, object> other)
        {
            throw new NotImplementedException();
        }

        public override IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public override object GetValue(object defaultVal = null, params object[] varNames)
        {
            throw new NotImplementedException();
        }

        public override bool Remove(KeyValuePair<string, object> item)
        {
            throw new NotImplementedException();
        }

        public override bool Remove(string key)
        {
            throw new NotImplementedException();
        }

        public override Dictionary<string, object> ToDictionary()
        {
            throw new NotImplementedException();
        }

        public override bool TryGetValue(string key, out object value)
        {
            throw new NotImplementedException();
        }

        public void Update(Dictionary<string, object> other)
        {
            // Update the settings in place.
            _values = other;
        }
        // Other methods and properties from BaseSettings class
    }
}
