using GraphQL;
using GraphQL.Types;
using Newtonsoft.Json;
using Graphql.AutoReflection.NetCore.Utilities;
using System;

namespace Graphql.AutoReflection.NetCore.Generic
{
    public class GenericMutationType : ObjectGraphType
    {
        public GenericMutationType(TableMetadata metaTable)
        {
            //Creacion de instancia con reflector
            //Type objectGraphType = typeof(ObjectGraphType<>).MakeGenericType(new Type[] { type });
            //dynamic tableType = Activator.CreateInstance(objectGraphType);

            var permission = metaTable.Type.Name.ToLower();
            this.RequirePermissions($"{permission}.add", $"{permission}.update", $"{permission}.delete");

            Name = metaTable.Type.Name;
            foreach (var tableColumn in metaTable.Columns)
            {
                InitGraphTableColumn(metaTable.Type, tableColumn);
            }
        }

        private void InitGraphTableColumn(Type parentType, ColumnMetadata columnMetadata)
        {
            if (columnMetadata.Type == typeof(NetTopologySuite.Geometries.Point) ||
                 columnMetadata.Type == typeof(NetTopologySuite.Geometries.Coordinate) ||
                 columnMetadata.Type == typeof(NetTopologySuite.Geometries.LineString) ||
                 columnMetadata.Type == typeof(NetTopologySuite.Geometries.MultiLineString))
            {
                Field(
                    typeof(string).GetGraphTypeFromType(true),
                    columnMetadata.ColumnName,
                    resolve: context =>
                    {
                        dynamic point = context.Source.GetPropertyValue(columnMetadata.ColumnName);
                        if (point == null) return null;
                        return JsonExtensions.SerializeWithGeoJson(point, formatting: Formatting.None);
                    }
                );

            }
            else
            {
                Field(
                    GraphUtils.ResolveGraphType(columnMetadata.Type),
                    columnMetadata.ColumnName
                );
            }
        }

    }
}
