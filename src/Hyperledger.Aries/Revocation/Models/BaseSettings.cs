using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.Revocation.Models
{
    public abstract class BaseSettings : IDictionary<string, object>
    {
        // Base settings class

        public abstract object GetValue(object defaultVal = null, params object[] varNames);

        public virtual bool? GetBool(bool? defaultVal = null, params string[] varNames)
        {
            var value = GetValue(varNames, defaultVal);
            if (value != null)
            {
                value = Convert.ToBoolean(value) && !new List<string> { "false", "False", "0" }.Contains(value.ToString());
            }

            return value as bool?;
        }

        public virtual int? GetInt(int? defaultVal = null, params string[] varNames)
        {
            var value = GetValue(varNames, defaultVal);
            if (value != null)
            {
                value = Convert.ToInt32(value);
            }

            return value as int?;
        }

        public virtual string GetStr(string defaultVal = null, params string[] varNames)
        {
            var value = GetValue(varNames, defaultVal);
            if (value != null)
            {
                value = value.ToString();
            }

            return value as string;
        }

        public abstract void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex);

        public abstract bool Contains(KeyValuePair<string, object> item);

        public abstract void Add(KeyValuePair<string, object> item);

        public abstract bool Remove(KeyValuePair<string, object> item);

        public abstract IEnumerator<KeyValuePair<string, object>> GetEnumerator();

        public abstract bool TryGetValue(string key, out object value);

        public abstract bool ContainsKey(string key);

        public abstract void Add(string key, object value);

        public abstract bool Remove(string key);

        public abstract void Clear();

        public abstract ICollection<string> Keys { get; }

        public abstract ICollection<object> Values { get; }

        public abstract bool IsReadOnly { get; }

        public abstract object this[string key] { get; set; }

        public abstract int Count { get; }

        public abstract IEnumerable<KeyValuePair<string, object>> Items { get; }

        public abstract BaseSettings Copy();

        public abstract BaseSettings Extend(IDictionary<string, object> other);

        public abstract Dictionary<string, object> ToDictionary();

        public override string ToString()
        {
            var items = string.Join(", ", Items);
            return $"<{GetType().Name}({items})>";
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}
