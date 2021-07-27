using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.Options;
using SER.Graphql.Reflection.NetCore.Builder;
using SER.Graphql.Reflection.NetCore.Utilities;
using SER.Models;
using System;
using System.Linq;

namespace SER.Graphql.Reflection.NetCore.Generic
{
    public class GenericInputType : InputObjectGraphType
    {
        private IDatabaseMetadata _dbMetadata;
        private ITableNameLookup _tableNameLookup;
        private readonly IOptionsMonitor<SERGraphQlOptions> _optionsDelegate;

        public GenericInputType(TableMetadata metaTable, IDatabaseMetadata dbMetadata, ITableNameLookup tableNameLookup, IOptionsMonitor<SERGraphQlOptions> optionsDelegate)
        {
            _dbMetadata = dbMetadata;
            _tableNameLookup = tableNameLookup;
            _optionsDelegate = optionsDelegate;

            Name = $"{metaTable.Type.Name.ToLower().ToSnakeCase()}_input";
            foreach (var tableColumn in metaTable.Columns)
            {
                InitGraphTableColumn(tableColumn, metaTable.Type);
            }
        }

        private void InitGraphTableColumn(ColumnMetadata columnMetadata, Type parentType)
        {
            //Console.WriteLine($"{columnMetadata.ColumnName} {columnMetadata.DataType}");
            if (columnMetadata.DataType == "uniqueidentifier") return;
            if (columnMetadata.IsJson)    // incluye litas de cada objeto
            {
                Field(
                   typeof(string).GetGraphTypeFromType(true),
                   columnMetadata.ColumnName,
                   resolve: context =>
                   {
                       var pi = parentType.GetProperty(columnMetadata.ColumnName);
                       dynamic value = pi.GetValue(context.Source);
                       if (value == null) return null;
                       return System.Text.Json.JsonSerializer.Serialize(value);
                   }
                );
            }
            else if (columnMetadata.IsList)    // incluye litas de cada objeto
            {
                var listObjectGraph = GetInternalListInstances(columnMetadata);
                AddField(new FieldType
                {
                    Name = columnMetadata.ColumnName,
                    ResolvedType = listObjectGraph
                    //Resolver = new CustomListResolver(mainTableColumn.Type, parentType, _httpContextAccessor)
                });
            }
            else if (typeof(IBaseModel).IsAssignableFrom(columnMetadata.Type)
                      || _dbMetadata.GetTableMetadatas().Any(x => x.Type == columnMetadata.Type))
            {
                AddField(new FieldType
                {
                    Name = columnMetadata.ColumnName,
                    ResolvedType = GetInternalInstances(columnMetadata)
                });
            }
            else if (columnMetadata.Type == typeof(NetTopologySuite.Geometries.Point) ||
                 columnMetadata.Type == typeof(NetTopologySuite.Geometries.Coordinate) ||
                 columnMetadata.Type == typeof(NetTopologySuite.Geometries.LineString) ||
                 columnMetadata.Type == typeof(NetTopologySuite.Geometries.MultiLineString))
            {
                Field(
                    typeof(string).GetGraphTypeFromType(true),
                    columnMetadata.ColumnName
                );

            }
            else if (columnMetadata.Type == typeof(TimeSpan))
            {
                Field(
                    typeof(string).GetGraphTypeFromType(true),
                    columnMetadata.ColumnName
               );
            }
            else if (columnMetadata.Type.IsEnum)
            {
                Field<IntGraphType>(columnMetadata.ColumnName, resolve: context =>
                {
                    var pi = parentType.GetProperty(columnMetadata.ColumnName);
                    return (int)pi.GetValue(context.Source);
                });
            }
            else if (columnMetadata.Type != _optionsDelegate.CurrentValue.UserType
                     && columnMetadata.Type != _optionsDelegate.CurrentValue.RoleType
                     && columnMetadata.Type != _optionsDelegate.CurrentValue.UserRoleType)
            {
                Field(
                    GraphUtils.ResolveGraphType(columnMetadata.Type),
                    columnMetadata.ColumnName
                );
            }
        }

        private dynamic GetInternalInstances(ColumnMetadata columnMetadata)
        {
            var metaTable = _dbMetadata.GetTableMetadatas().FirstOrDefault(x => x.Type.Name == columnMetadata.Type.Name);

            string key = $"{metaTable.Type.Name.ToLower().ToSnakeCase()}_first_internal_input";
            dynamic objectGraphType = null;

            if (!_tableNameLookup.ExistInputGraphType(key))
            {
                var inherateListType = typeof(InputObjectGraphType<>).MakeGenericType(new Type[] { columnMetadata.Type });
                objectGraphType = Activator.CreateInstance(inherateListType);
                objectGraphType.Name = key;
                foreach (var tableColumn in metaTable.Columns)
                {
                    objectGraphType.Field(
                        GraphUtils.ResolveGraphType(tableColumn.Type),
                        tableColumn.ColumnName
                    );
                }
            }
            return _tableNameLookup.GetOrInsertInputGraphType(key, objectGraphType);
        }

        private dynamic GetInternalListInstances(ColumnMetadata columnMetadata)
        {
            var metaTable = _dbMetadata.GetTableMetadatas().FirstOrDefault(x => x.Type.Name == columnMetadata.Type.Name);

            string key = $"{metaTable.Type.Name.ToLower().ToSnakeCase()}_list_input";
            var objectGraphType = new InputObjectGraphType();
            objectGraphType.Name = key;
            dynamic listGraphType = null;

            if (!_tableNameLookup.ExistInputListGraphType(key))
            {
                var tableType = GetSecondGraphType(columnMetadata, metaTable);
                var inherateListType = typeof(ListGraphType<>).MakeGenericType(new Type[] { tableType.GetType() });
                listGraphType = Activator.CreateInstance(inherateListType);
                listGraphType.ResolvedType = tableType;
                // Field<ListGraphType<CityType>>(nameof(State.cities));
            }
            return _tableNameLookup.GetOrInsertInputListGraphType(key, listGraphType);
        }

        private dynamic GetSecondGraphType(ColumnMetadata columnMetadata, TableMetadata metaTable = null)
        {
            string key = $"{columnMetadata.Type.Name}_internal_input";
            dynamic objectGraphType = null;
            if (metaTable == null)
                metaTable = _dbMetadata.GetTableMetadatas().FirstOrDefault(x => x.Type.Name == columnMetadata.Type.Name);
            if (!_tableNameLookup.ExistInputGraphType(key))
            {
                //Creacion de instancia
                var inherateListType = typeof(InputObjectGraphType<>).MakeGenericType(new Type[] { columnMetadata.Type });
                objectGraphType = Activator.CreateInstance(inherateListType);
                objectGraphType.Name = key;
                foreach (var tableColumn in metaTable.Columns)
                {
                    objectGraphType.Field(
                        GraphUtils.ResolveGraphType(tableColumn.Type),
                        tableColumn.ColumnName
                    );
                }
            }
            return _tableNameLookup.GetOrInsertInputGraphType(key, objectGraphType);
        }
    }
}
