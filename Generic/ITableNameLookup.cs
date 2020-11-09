using GraphQL.Types;
using System;
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

        ListGraphType<ObjectGraphType> GetOrInsertListGraphType(string key, ListGraphType<ObjectGraphType> objectGraphType);
        ListGraphType<InputObjectGraphType> GetOrInsertInputListGraphType(string key, ListGraphType<InputObjectGraphType> objectGraphType);
        bool ExistListGraphType(string key);
        bool ExistInputListGraphType(string key);

        string GetFriendlyName(string correctName);
    }

    public class TableNameLookup : ITableNameLookup
    {
        private IDictionary<string, string> _lookupTable = new Dictionary<string, string>();
        private IDictionary<string, dynamic> _graphTypeDict = new Dictionary<string, dynamic>();
        private IDictionary<string, dynamic> _inputGraphTypeDict = new Dictionary<string, dynamic>();
        private IDictionary<string, ListGraphType<ObjectGraphType>> _listGraphTypeDict = new Dictionary<string, ListGraphType<ObjectGraphType>>();
        private IDictionary<string, ListGraphType<InputObjectGraphType>> _inputListGraphTypeDict = new Dictionary<string, ListGraphType<InputObjectGraphType>>();

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
                _graphTypeDict.Add(key, objectGraphType);
            }
            return _graphTypeDict[key];
        }

        public dynamic GetOrInsertInputGraphType(string key, dynamic objectGraphType)
        {
            if (!_inputGraphTypeDict.ContainsKey(key))
            {
                // Console.WriteLine("Table agregada en diccionario cache: " + key);
                _inputGraphTypeDict.Add(key, objectGraphType);
            }
            return _inputGraphTypeDict[key];
        }

        public bool InsertKeyName(string correctName)
        {
            if (!_lookupTable.ContainsKey(correctName))
            {
                var friendlyName = StringExt.CanonicalName(correctName);
                _lookupTable.Add(correctName, friendlyName);
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
        
        public ListGraphType<ObjectGraphType> GetOrInsertListGraphType(string key, ListGraphType<ObjectGraphType> objectGraphType)
        {
            if (!_listGraphTypeDict.ContainsKey(key))
            {
                // Console.WriteLine("Table agregada en diccionario lista cache: " + key);
                _listGraphTypeDict.Add(key, objectGraphType);
            }
            return _listGraphTypeDict[key];
        }

        public bool ExistListGraphType(string key)
        {
            return _listGraphTypeDict.ContainsKey(key);
        }

        public ListGraphType<InputObjectGraphType> GetOrInsertInputListGraphType(string key, ListGraphType<InputObjectGraphType> objectGraphType)
        {
            if (!_inputListGraphTypeDict.ContainsKey(key))
            {
                // Console.WriteLine("Table agregada en diccionario lista cache: " + key);
                _inputListGraphTypeDict.Add(key, objectGraphType);
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
