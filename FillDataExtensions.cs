using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore
{
    public class FillDataExtensions
    {
        private ConcurrentDictionary<string, object> _extensionsDict = new ConcurrentDictionary<string, object>();

        public void Add(string key, object value)
        {
            if (!_extensionsDict.ContainsKey(key))
                _extensionsDict.TryAdd(key, value);
            else
            {
                var index = 1;
                do
                {
                    key = $"{key}_{index}";
                    index++;
                } while (_extensionsDict.ContainsKey(key));
                _extensionsDict.TryAdd(key, value);
            }
        }

        public ConcurrentDictionary<string, object> GetAll()
        {
            return _extensionsDict;
        }


        public void Clear() => _extensionsDict.Clear();
    }
}
