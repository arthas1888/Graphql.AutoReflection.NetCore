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
        private string _nameField;

        public PointResolver(string nameField)
        {
            _nameField = nameField;
        }

        public object Resolve(IResolveFieldContext context)
        {
            dynamic point = context.Source.GetPropertyValue(_nameField);
            if (point == null) return null;
            return JsonExtensions.SerializeWithGeoJson(point, formatting: Formatting.None);

        }

    }
}
