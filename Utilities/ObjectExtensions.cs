using SER.Graphql.Reflection.NetCore.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore.Utilities
{
    public static class ObjectExtensions
    {
        public static T DictToObject<T>(this IDictionary<string, object> source)
            where T : class // , new()
        {
            var someObject = Activator.CreateInstance(typeof(T)); // new T();
            var someObjectType = someObject.GetType();

            foreach (var item in source)
            {
                someObjectType
                         .GetProperty(item.Key)
                         .SetValue(someObject, item.Value.GetRealValue(), null);
            }

            return someObject as T;
        }

        public static IDictionary<string, object> AsDictionary(this object source, BindingFlags bindingAttr = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
        {
            return source.GetType().GetProperties(bindingAttr).ToDictionary
            (
                propInfo => propInfo.Name,
                propInfo => propInfo.GetValue(source, null)
            );

        }
    }
}
