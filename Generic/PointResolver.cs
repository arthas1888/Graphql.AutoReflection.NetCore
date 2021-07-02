using GraphQL;
using GraphQL.Resolvers;
using GraphQL.Types;
using NetTopologySuite.Geometries;
using Newtonsoft.Json;
using SER.Graphql.Reflection.NetCore.Utilities;
using System;

namespace SER.Graphql.Reflection.NetCore.Generic
{
    public class PointResolver : IFieldResolver
    {
        private Type _typeField;

        public PointResolver(Type typeField)
        {
            _typeField = typeField;
        }

        public object Resolve(IResolveFieldContext context)
        {
            dynamic point = context.Source.GetPropertyValue(_typeField);
            if (point == null) return null;
            return JsonExtensions.SerializeWithGeoJson(point, formatting: Formatting.None);

        }

    }
}
