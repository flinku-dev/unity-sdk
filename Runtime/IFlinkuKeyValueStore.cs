using System.Collections.Generic;

namespace Flinku
{
    public interface IFlinkuKeyValueStore
    {
        bool GetBool(string key, bool defaultValue = false);
        void SetBool(string key, bool value);
        string GetString(string key, string defaultValue = null);
        void SetString(string key, string value);
        void RemoveKey(string key);
        IEnumerable<string> AllKeys();
    }

    public sealed class InMemoryKeyValueStore : IFlinkuKeyValueStore
    {
        private readonly Dictionary<string, string> _strings = new();
        private readonly Dictionary<string, bool> _bools = new();

        public bool GetBool(string key, bool defaultValue = false) =>
            _bools.TryGetValue(key, out var v) ? v : defaultValue;

        public void SetBool(string key, bool value) => _bools[key] = value;

        public string GetString(string key, string defaultValue = null) =>
            _strings.TryGetValue(key, out var v) ? v : defaultValue;

        public void SetString(string key, string value) => _strings[key] = value;

        public void RemoveKey(string key)
        {
            _strings.Remove(key);
            _bools.Remove(key);
        }

        public IEnumerable<string> AllKeys()
        {
            var keys = new HashSet<string>(_strings.Keys);
            foreach (var k in _bools.Keys) keys.Add(k);
            return keys;
        }
    }
}
