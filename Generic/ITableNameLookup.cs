using GraphQL.Types;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore.Generic
{
    public interface ITableNameLookup
    {
        bool InsertKeyName(string friendlyName);
        dynamic GetOrInsertGraphType(string key, dynamic objectGraphType);
        dynamic GetOrInsertInputGraphType(string key, dynamic objectGraphType);
        bool ExistGraphType(string key);
        bool ExistInputGraphType(string key);

        dynamic GetOrInsertListGraphType(string key, dynamic listGraphType);
        dynamic GetOrInsertSecondListGraphType(string key, dynamic listGraphType);
        dynamic GetOrInsertInputListGraphType(string key, dynamic listGraphType);
        bool ExistListGraphType(string key);
        bool ExistSecondListGraphType(string key);
        bool ExistInputListGraphType(string key);

        string GetFriendlyName(string correctName);
    }

    public class TableNameLookup : ITableNameLookup
    {
        private readonly ConcurrentDictionary<string, string> _lookupTable = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, dynamic> _graphTypeDict = new ConcurrentDictionary<string, dynamic>();
        private readonly ConcurrentDictionary<string, dynamic> _inputGraphTypeDict = new ConcurrentDictionary<string, dynamic>();
        private readonly ConcurrentDictionary<string, dynamic> _listGraphTypeDict = new ConcurrentDictionary<string, dynamic>();
        private readonly ConcurrentDictionary<string, dynamic> _secondListGraphTypeDict = new ConcurrentDictionary<string, dynamic>();
        private readonly ConcurrentDictionary<string, dynamic> _inputListGraphTypeDict = new ConcurrentDictionary<string, dynamic>();

        public bool ExistGraphType(string key)
        {
            return _graphTypeDict.ContainsKey(key);
        }

        public bool ExistInputGraphType(string key)
        {
            return _inputGraphTypeDict.ContainsKey(key);
        }

        public dynamic GetOrInsertGraphType(string key, dynamic objectGraphType)
        {
            if (!_graphTypeDict.ContainsKey(key))
            {
                // Console.WriteLine("Table agregada en diccionario cache: " + key);
                try
                {
                    _graphTypeDict.TryAdd(key, objectGraphType);
                }
                catch (Exception) { };
            }
            return _graphTypeDict[key];
        }

        public dynamic GetOrInsertInputGraphType(string key, dynamic objectGraphType)
        {
            if (!_inputGraphTypeDict.ContainsKey(key))
            {
                // Console.WriteLine("Table agregada en diccionario cache: " + key);
                try
                {
                    _inputGraphTypeDict.TryAdd(key, objectGraphType);
                }
                catch (Exception) { };
            }
            return _inputGraphTypeDict[key];
        }

        public bool InsertKeyName(string correctName)
        {
            if (!_lookupTable.ContainsKey(correctName))
            {
                var friendlyName = StringExt.CanonicalName(correctName);
                _lookupTable.TryAdd(correctName, friendlyName);
                return true;
            }
            return false;
        }

        public string GetFriendlyName(string correctName)
        {
            if (!_lookupTable.TryGetValue(correctName, out string value))
                throw new Exception($"Could not get {correctName} out of the list.");
            return value;
        }

        public dynamic GetOrInsertListGraphType(string key, dynamic listGraphType)
        {
            if (!_listGraphTypeDict.ContainsKey(key))
            {
                // Console.WriteLine("Table agregada en diccionario lista cache: " + key);
                try
                {
                    _listGraphTypeDict.TryAdd(key, listGraphType);
                }
                catch (Exception) { };
            }
            return _listGraphTypeDict[key];
        }

        public dynamic GetOrInsertSecondListGraphType(string key, dynamic listGraphType)
        {
            if (!_secondListGraphTypeDict.ContainsKey(key))
            {
                // Console.WriteLine("Table agregada en diccionario lista cache: " + key);
                try
                {
                    _secondListGraphTypeDict.TryAdd(key, listGraphType);
                }
                catch (Exception) { };
            }
            return _secondListGraphTypeDict[key];
        }

        public bool ExistListGraphType(string key)
        {
            return _listGraphTypeDict.ContainsKey(key);
        }

        public bool ExistSecondListGraphType(string key)
        {
            return _secondListGraphTypeDict.ContainsKey(key);
        }

        public dynamic GetOrInsertInputListGraphType(string key, dynamic objectGraphType)
        {
            if (!_inputListGraphTypeDict.ContainsKey(key))
            {
                // Console.WriteLine("Table agregada en diccionario lista cache: " + key);
                try
                {
                    _inputListGraphTypeDict.TryAdd(key, objectGraphType);
                }
                catch (Exception) { };
            }
            return _inputListGraphTypeDict[key];
        }

        public bool ExistInputListGraphType(string key)
        {
            return _inputListGraphTypeDict.ContainsKey(key);
        }
    }

    public static class StringExt
    {
        public static string CanonicalName(string correctName)
        {
            var index = correctName.LastIndexOf("_");
            var result = correctName.Substring(index + 1, correctName.Length - index - 1);
            return char.ToLowerInvariant(result[0]) + result.Substring(1);
        }
    }
}
