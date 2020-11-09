using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Graphql.AutoReflection.NetCore
{
    public class FillDataExtensions
    {
        private Dictionary<string, object> _extensionsDict = new Dictionary<string, object>();

        public void Add(string key, object value)
        {
            if (!_extensionsDict.ContainsKey(key))
                _extensionsDict.Add(key, value);
            else
            {
                var index = 1;
                do
                {
                    key = $"{key}_{index}";
                    index++;
                } while (_extensionsDict.ContainsKey(key));
                _extensionsDict.Add(key, value);
            }
        }

        public Dictionary<string, object> GetAll()
        {
            return _extensionsDict;
        }

    }
}
