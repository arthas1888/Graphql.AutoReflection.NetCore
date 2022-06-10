using GraphQL;
using GraphQL.Resolvers;
using GraphQL.Types;
using NetTopologySuite.Geometries;
using Newtonsoft.Json;
using SER.Graphql.Reflection.NetCore.Utilities;
using System;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore.Generic
{
    public class PointResolver : IFieldResolver
    {
        private Type _typeField;
        private string _fieldName;

        public PointResolver(Type typeField, string fieldName)
        {
            _typeField = typeField;
            _fieldName = fieldName;
        }

        public object Resolve(IResolveFieldContext context)
        {
            var pi = _typeField.GetProperty(_fieldName);
            dynamic point = pi.GetValue(context.Source);
            if (point == null) return null;
            return JsonExtensions.SerializeWithGeoJson(point, formatting: Formatting.None);

        }

        public ValueTask<object> ResolveAsync(IResolveFieldContext context)
        {
            throw new NotImplementedException();
        }
    }
}
