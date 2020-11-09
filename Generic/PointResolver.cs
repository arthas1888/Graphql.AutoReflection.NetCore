using GraphQL;
using GraphQL.Resolvers;
using GraphQL.Types;
using NetTopologySuite.Geometries;
using Newtonsoft.Json;
using Graphql.AutoReflection.NetCore.Utilities;
using System;

namespace Graphql.AutoReflection.NetCore.Generic
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
            Point point = (Point)context.Source.GetPropertyValue(_nameField);
            Console.WriteLine($"point {point} {context.Source.GetPropertyValue(_nameField).GetType()}");
            return JsonExtensions.SerializeWithGeoJson<Point>(point, formatting: Formatting.None);
        }

    }
}
