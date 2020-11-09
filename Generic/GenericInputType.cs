using GraphQL;
using GraphQL.Types;
using SER.Graphql.Reflection.NetCore.Utilities;
using System;
using System.Linq;

namespace SER.Graphql.Reflection.NetCore.Generic
{
    public class GenericInputType : InputObjectGraphType
    {
        private IDatabaseMetadata _dbMetadata;
        private ITableNameLookup _tableNameLookup;

        public GenericInputType(TableMetadata metaTable, IDatabaseMetadata dbMetadata, ITableNameLookup tableNameLookup)
        {
            _dbMetadata = dbMetadata;
            _tableNameLookup = tableNameLookup;

            Name = $"{metaTable.Type.Name.ToLower().ToSnakeCase()}_input";
            foreach (var tableColumn in metaTable.Columns)
            {
                InitGraphTableColumn(tableColumn);
            }
        }

        private void InitGraphTableColumn(ColumnMetadata columnMetadata)
        {
            //Console.WriteLine($"{columnMetadata.ColumnName} {columnMetadata.DataType}");
            if (columnMetadata.DataType == "uniqueidentifier") return;
            if (columnMetadata.IsList)    // incluye litas de cada objeto
            {
                var listObjectGraph = GetInternalListInstances(columnMetadata);
                AddField(new FieldType
                {
                    Name = columnMetadata.ColumnName,
                    ResolvedType = listObjectGraph
                    //Resolver = new CustomListResolver(mainTableColumn.Type, parentType, _httpContextAccessor)
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
                Field<IntGraphType>(columnMetadata.ColumnName, resolve: context => (int)context.Source.GetPropertyValue(columnMetadata.ColumnName));
            }
            else if (!Constantes.SystemTablesSingular.Contains(columnMetadata.Type.Name))
            {
                Field(
                    GraphUtils.ResolveGraphType(columnMetadata.Type),
                    columnMetadata.ColumnName
                );
            }
        }

        private ListGraphType<InputObjectGraphType> GetInternalListInstances(ColumnMetadata columnMetadata)
        {
            var metaTable = _dbMetadata.GetTableMetadatas().FirstOrDefault(x => x.Type.Name == columnMetadata.Type.Name);

            string key = $"{metaTable.Type.Name.ToLower().ToSnakeCase()}_list_input";
            var objectGraphType = new InputObjectGraphType();
            objectGraphType.Name = key;
            ListGraphType<InputObjectGraphType> listGraphType = null;

            if (!_tableNameLookup.ExistInputListGraphType(key))
            {
                var tableType = GetSecondGraphType(columnMetadata, metaTable);
                listGraphType = new ListGraphType<InputObjectGraphType>();
                listGraphType.ResolvedType = tableType;
                // Field<ListGraphType<CityType>>(nameof(State.cities));
            }
            return _tableNameLookup.GetOrInsertInputListGraphType(key, listGraphType);
        }

        private InputObjectGraphType GetSecondGraphType(ColumnMetadata columnMetadata, TableMetadata metaTable = null)
        {
            string key = $"{columnMetadata.Type.Name}_internal_input";
            InputObjectGraphType objectGraphType = null;
            if (metaTable == null)
                metaTable = _dbMetadata.GetTableMetadatas().FirstOrDefault(x => x.Type.Name == columnMetadata.Type.Name);
            if (!_tableNameLookup.ExistInputGraphType(key))
            {
                //Creacion de instancia
                objectGraphType = new InputObjectGraphType();
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
