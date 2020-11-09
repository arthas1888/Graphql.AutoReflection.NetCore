using Graphql.Reflection.NetCore.Generic;
using Graphql.Reflection.NetCore.Models;
using GraphQL;
using GraphQL.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Reflection;
using Newtonsoft.Json;
using Graphql.Reflection.NetCore.Utilities;
using GraphQL.DataLoader;
using NetTopologySuite.Geometries;
using GraphQL.Resolvers;

namespace Graphql.Reflection.NetCore
{
    public class TableType<T> : ObjectGraphType<T>
    {
        private IDatabaseMetadata _dbMetadata;
        private ITableNameLookup _tableNameLookup;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IDataLoaderContextAccessor _accessor;
        private bool _crud;

        public QueryArguments TableArgs { get; set; }

        public TableType(
            TableMetadata mainTable,
            IDatabaseMetadata dbMetadata,
            ITableNameLookup tableNameLookup,
            IHttpContextAccessor httpContextAccessor,
            IDataLoaderContextAccessor accessor,
            bool crud = false)
        {
            _crud = crud;
            _tableNameLookup = tableNameLookup;
            _dbMetadata = dbMetadata;
            _accessor = accessor;
            _httpContextAccessor = httpContextAccessor;
            var permission = mainTable.Type.Name.ToLower();
            var friendlyTableName = _tableNameLookup.GetFriendlyName(mainTable.TableName);
            if (crud)
                this.RequirePermissions($"{permission}.add", $"{permission}.update", $"{permission}.delete");
            else
                this.ValidatePermissions(permission, friendlyTableName, mainTable.Type.Name);

            Name = mainTable.TableName;

            foreach (var mainTableColumn in mainTable.Columns)
            {
                InitMainGraphTableColumn(mainTable.Type, mainTableColumn);
            }
        }

        private void InitMainGraphTableColumn(Type parentType, ColumnMetadata mainTableColumn)
        {
            //if (parentType.Name == "Customer")
            //    Console.WriteLine($"{mainTableColumn.ColumnName} GraphType: {GraphUtils.ResolveGraphType(mainTableColumn.Type)} Type: {mainTableColumn.Type} IsList {mainTableColumn.IsList}");
            // instancias internas
            if (mainTableColumn.IsList)    // incluye litas de cada objeto
            {
                var queryThirdArguments = new QueryArguments();
                queryThirdArguments.Add(new QueryArgument<IntGraphType> { Name = "first" });
                queryThirdArguments.Add(new QueryArgument<StringGraphType> { Name = "orderBy" });
                queryThirdArguments.Add(new QueryArgument<StringGraphType> { Name = "all" });
                queryThirdArguments.Add(new QueryArgument<BooleanGraphType> { Name = "join" });

                var listObjectGraph = GetInternalListInstances(parentType, mainTableColumn, queryThirdArguments: queryThirdArguments);

                var inherateType = typeof(CustomListResolver<>).MakeGenericType(new Type[] { mainTableColumn.Type });
                dynamic resolver = Activator.CreateInstance(inherateType, new object[] { mainTableColumn.Type, parentType, _httpContextAccessor, _accessor });

                AddField(new FieldType
                {
                    Name = $"{mainTableColumn.ColumnName}",
                    ResolvedType = listObjectGraph,
                    Arguments = queryThirdArguments,
                    Resolver = resolver
                });
            }
            else if (typeof(IBaseModel).IsAssignableFrom(mainTableColumn.Type)
                        || Constantes.SystemTablesSingular.Contains(mainTableColumn.Type.Name))
            {
                GetInternalInstances(mainTableColumn);
            }
            else if (mainTableColumn.Type == typeof(Point) ||
                 mainTableColumn.Type == typeof(Coordinate) ||
                 mainTableColumn.Type == typeof(LineString) ||
                 mainTableColumn.Type == typeof(MultiLineString))
            {
                Field(
                    typeof(string).GetGraphTypeFromType(true),
                    mainTableColumn.ColumnName,
                    resolve: context =>
                    {
                        dynamic point = context.Source.GetPropertyValue(mainTableColumn.ColumnName);
                        if (point == null) return null;
                        return JsonExtensions.SerializeWithGeoJson(point, formatting: Formatting.None);
                    }
               );
                FillArgs(mainTableColumn.ColumnName, mainTableColumn.Type);
            }
            else if (mainTableColumn.Type == typeof(TimeSpan))
            {
                Field(
                    typeof(string).GetGraphTypeFromType(true),
                    mainTableColumn.ColumnName,
                    resolve: context =>
                    {
                        var value = context.Source.GetPropertyValue(mainTableColumn.ColumnName);
                        if (value == null) return null;
                        return ((TimeSpan)value).ToString();
                    }
               );
                FillArgs(mainTableColumn.ColumnName, mainTableColumn.Type);
            }
            else
            {
                Field(
                    GraphUtils.ResolveGraphType(mainTableColumn.Type),
                    mainTableColumn.ColumnName
                );
                FillArgs(mainTableColumn.ColumnName, mainTableColumn.Type);

                if (mainTableColumn.Type.IsEnum)
                {
                    Field<IntGraphType>($"{mainTableColumn.ColumnName}_value", resolve: context => (int)context.Source.GetPropertyValue(mainTableColumn.ColumnName));
                }
            }

        }

        private void InitGraphTableColumn(Type parentType, ColumnMetadata columnMetadata, dynamic objectGraphType, QueryArguments queryArguments)
        {
            if (typeof(IBaseModel).IsAssignableFrom(columnMetadata.Type)
                    || Constantes.SystemTablesSingular.Contains(columnMetadata.Type.Name))
            {
                var queryThirdArguments = new QueryArguments();
                queryThirdArguments.Add(new QueryArgument<StringGraphType> { Name = "all" });
                var metaTable = _dbMetadata.GetTableMetadatas().FirstOrDefault(x => x.Type.Name == columnMetadata.Type.Name);
                var tableType = GetThirdGraphType(metaTable, columnMetadata, queryThirdArguments);

                objectGraphType.AddField(new FieldType
                {
                    Name = $"{columnMetadata.ColumnName}",
                    ResolvedType = tableType,
                    Arguments = queryThirdArguments
                });
            }
            // incluye listas de cada objeto
            else if (columnMetadata.IsList)
            {
                try
                {
                    var queryThirdArguments = new QueryArguments();
                    queryThirdArguments.Add(new QueryArgument<StringGraphType> { Name = "all" });

                    var listObjectGraph = GetInternalListInstances(parentType, columnMetadata, queryThirdArguments: queryThirdArguments);

                    objectGraphType.AddField(new FieldType
                    {
                        Name = $"{columnMetadata.ColumnName}",
                        ResolvedType = listObjectGraph,
                        Arguments = queryThirdArguments
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    objectGraphType.Field(
                      GraphUtils.ResolveGraphType(columnMetadata.Type),
                        columnMetadata.ColumnName
                    );
                    FillArguments(queryArguments, columnMetadata.ColumnName, columnMetadata.Type);
                }
            }
            else if (columnMetadata.Type == typeof(TimeSpan))
            {
                objectGraphType.AddField(
                    new FieldType
                    {
                        Type = typeof(string).GetGraphTypeFromType(true),
                        Name = columnMetadata.ColumnName,
                        Resolver = new TimeSpanResolver(columnMetadata.ColumnName)
                    }
               );
                FillArguments(queryArguments, columnMetadata.ColumnName, columnMetadata.Type);
            }
            else
            {
                objectGraphType.Field(
                    GraphUtils.ResolveGraphType(columnMetadata.Type),
                    columnMetadata.ColumnName
                );
                FillArguments(queryArguments, columnMetadata.ColumnName, columnMetadata.Type);
            }
        }


        private void GetInternalInstances(ColumnMetadata mainTableColumn)
        {
            string key = $"Internal_{mainTableColumn.Type.Name}";
            var queryArguments = new QueryArguments();
            queryArguments.Add(new QueryArgument<StringGraphType> { Name = "all" });
            var metaTable = _dbMetadata.GetTableMetadatas().FirstOrDefault(x => x.Type.Name == mainTableColumn.Type.Name);
            var tableType = GetSecondGraphType(mainTableColumn, queryArguments, metaTable);
            // Field<StateType>(nameof(City.state));
            AddField(new FieldType
            {
                Name = $"{mainTableColumn.ColumnName}",
                ResolvedType = tableType,
                Arguments = queryArguments
            });
        }

        private ListGraphType<ObjectGraphType> GetInternalListInstances(Type parentType, ColumnMetadata columnMetadata,
            QueryArguments queryThirdArguments = null)
        {
            var metaTable = _dbMetadata.GetTableMetadatas().FirstOrDefault(x => x.Type.Name == columnMetadata.Type.Name);

            ListGraphType<ObjectGraphType> listGraphType = null;
            if (!_tableNameLookup.ExistListGraphType(columnMetadata.ColumnName))
            {
                var tableType = GetSecondGraphType(columnMetadata, queryThirdArguments, metaTable);
                listGraphType = new ListGraphType<ObjectGraphType>();
                listGraphType.ResolvedType = tableType;
                // Field<ListGraphType<CityType>>(nameof(State.cities));
            }
            else
            {
                foreach (var tableColumn in metaTable.Columns)
                {
                    FillArguments(queryThirdArguments, tableColumn.ColumnName, tableColumn.Type);
                }
            }

            return _tableNameLookup.GetOrInsertListGraphType(columnMetadata.ColumnName, listGraphType);

        }

        private dynamic GetSecondGraphType(ColumnMetadata columnMetadata, QueryArguments queryArguments, TableMetadata metaTable = null)
        {
            string key = $"Internal_{columnMetadata.Type.Name}";
            dynamic objectGraphType = null;
            if (metaTable == null)
                metaTable = _dbMetadata.GetTableMetadatas().FirstOrDefault(x => x.Type.Name == columnMetadata.Type.Name);
            if (!_tableNameLookup.ExistGraphType(key))
            {
                //Creacion de instancia
                //objectGraphType = new ObjectGraphType();
                var inherateType = typeof(ObjectGraphType<>).MakeGenericType(new Type[] { columnMetadata.Type });

                objectGraphType = Activator.CreateInstance(inherateType);
                objectGraphType.Name = key;
                var permission = columnMetadata.Type.Name.ToLower();
                var friendlyTableName = StringExt.CanonicalName(columnMetadata.Type.Name.ToSnakeCase());
                //if (!_crud)
                //    objectGraphType.ValidatePermissions(permission, friendlyTableName, columnMetadata.DataType);
                //if (!typesWithoutPermission.Contains(permission) &&
                //    !typesWithoutPermission.Contains(friendlyTableName))
                //{
                //    if (Constantes.SystemTablesSingular.Contains(columnMetadata.DataType))
                //        objectGraphType.RequirePermissions($"{friendlyTableName}.view");
                //    else
                //        objectGraphType.RequirePermissions($"{permission}.view");
                //}
                foreach (var tableColumn in metaTable.Columns)
                {
                    InitGraphTableColumn(metaTable.Type, tableColumn, objectGraphType, queryArguments);
                }
            }
            else
            {
                foreach (var tableColumn in metaTable.Columns)
                {
                    FillArguments(queryArguments, tableColumn.ColumnName, tableColumn.Type);
                }
            }
            return _tableNameLookup.GetOrInsertGraphType(key, objectGraphType);
        }

        private dynamic GetThirdGraphType(TableMetadata metaTable, ColumnMetadata columnMetadata, QueryArguments queryArguments)
        {
            string key = $"Third_{columnMetadata.Type.Name}";
            dynamic objectGraphInternal = null;
            if (!_tableNameLookup.ExistGraphType(key))
            {
                var inherateType = typeof(ObjectGraphType<>).MakeGenericType(new Type[] { metaTable.Type });

                objectGraphInternal = Activator.CreateInstance(inherateType);
                objectGraphInternal.Name = key;
                var permission = columnMetadata.Type.Name.ToLower();
                var friendlyTableName = StringExt.CanonicalName(columnMetadata.Type.Name.ToSnakeCase());
                // habilitar permisos para tablas internas
                //if (!_crud)
                //    objectGraphInternal.ValidatePermissions(permission, friendlyTableName, columnMetadata.DataType);

                foreach (var tableColumn in metaTable.Columns)
                {
                    objectGraphInternal.Field(
                      GraphUtils.ResolveGraphType(tableColumn.Type),
                        tableColumn.ColumnName
                    );
                    FillArguments(queryArguments, tableColumn.ColumnName, tableColumn.Type);
                }
            }
            else
            {
                foreach (var tableColumn in metaTable.Columns)
                {
                    FillArguments(queryArguments, tableColumn.ColumnName, tableColumn.Type);
                }
            }
            return _tableNameLookup.GetOrInsertGraphType(key, objectGraphInternal);
        }

        private void FillArgs(string columnName, Type type)
        {
            if (TableArgs == null)
            {
                TableArgs = new QueryArguments();
                TableArgs.Add(new QueryArgument<IntGraphType> { Name = "first" });
                TableArgs.Add(new QueryArgument<IntGraphType> { Name = "page" });
                TableArgs.Add(new QueryArgument<StringGraphType> { Name = "orderBy" });
                TableArgs.Add(new QueryArgument<StringGraphType> { Name = "all" });
            }
            if (type.IsArray)
            {
                TableArgs.Add(new QueryArgument<StringGraphType> { Name = $"{columnName}_ext" });
            }
            if (columnName == "id")
            {
                TableArgs.Add(new QueryArgument<IdGraphType> { Name = "id" });
                TableArgs.Add(new QueryArgument<StringGraphType> { Name = "id_iext" });
                TableArgs.Add(new QueryArgument<StringGraphType> { Name = "id_iext_or" });
            }
            else
            {
                var queryArgument = new QueryArgument(GraphUtils.ResolveGraphType(type)) { Name = columnName };
                TableArgs.Add(queryArgument);

                if (type == typeof(DateTime?) || type == typeof(DateTime))
                {
                    TableArgs.Add(new QueryArgument<DateTimeGraphType> { Name = $"{columnName}_gt" });
                    TableArgs.Add(new QueryArgument<DateTimeGraphType> { Name = $"{columnName}_gte" });
                    TableArgs.Add(new QueryArgument<DateTimeGraphType> { Name = $"{columnName}_lt" });
                    TableArgs.Add(new QueryArgument<DateTimeGraphType> { Name = $"{columnName}_lte" });
                }
                else if (type == typeof(int?) || type == typeof(int) || type == typeof(decimal?) || type == typeof(decimal)
                    || type == typeof(double?) || type == typeof(double) || type == typeof(float?) || type == typeof(float))
                {
                    TableArgs.Add(new QueryArgument<IntGraphType> { Name = $"{columnName}_gt" });
                    TableArgs.Add(new QueryArgument<IntGraphType> { Name = $"{columnName}_gte" });
                    TableArgs.Add(new QueryArgument<IntGraphType> { Name = $"{columnName}_lt" });
                    TableArgs.Add(new QueryArgument<IntGraphType> { Name = $"{columnName}_lte" });
                    TableArgs.Add(new QueryArgument<StringGraphType> { Name = $"{columnName}_iext" });
                    TableArgs.Add(new QueryArgument<StringGraphType> { Name = $"{columnName}_iext_or" });
                    TableArgs.Add(new QueryArgument<BooleanGraphType> { Name = $"{columnName}_isnull" });
                }
                else if (type != typeof(bool))
                {
                    TableArgs.Add(new QueryArgument<StringGraphType> { Name = $"{columnName}_iext" });
                    TableArgs.Add(new QueryArgument<StringGraphType> { Name = $"{columnName}_iext_or" });
                    TableArgs.Add(new QueryArgument<BooleanGraphType> { Name = $"{columnName}_isnull" });
                }
            }

        }

        private void FillArguments(QueryArguments queryArguments, string columnName, Type type)
        {
            if (queryArguments == null) return;
            if (type.IsArray)
            {
                queryArguments.Add(new QueryArgument<StringGraphType> { Name = $"{columnName}_ext" });
            }
            if (columnName == "id")
            {
                queryArguments.Add(new QueryArgument<IdGraphType> { Name = "id" });
                queryArguments.Add(new QueryArgument<StringGraphType> { Name = "id_iext" });
                queryArguments.Add(new QueryArgument<StringGraphType> { Name = "id_iext_or" });
            }
            else
            {
                var queryArgument = new QueryArgument(GraphUtils.ResolveGraphType(type)) { Name = columnName };
                queryArguments.Add(queryArgument);
                if (type == typeof(DateTime?) || type == typeof(DateTime))
                {
                    queryArguments.Add(new QueryArgument<DateTimeGraphType> { Name = $"{columnName}_gt" });
                    queryArguments.Add(new QueryArgument<DateTimeGraphType> { Name = $"{columnName}_gte" });
                    queryArguments.Add(new QueryArgument<DateTimeGraphType> { Name = $"{columnName}_lt" });
                    queryArguments.Add(new QueryArgument<DateTimeGraphType> { Name = $"{columnName}_lte" });
                }
                else if (type == typeof(int?) || type == typeof(int) || type == typeof(decimal?) || type == typeof(decimal)
                    || type == typeof(double?) || type == typeof(double) || type == typeof(float?) || type == typeof(float))
                {
                    queryArguments.Add(new QueryArgument<IntGraphType> { Name = $"{columnName}_gt" });
                    queryArguments.Add(new QueryArgument<IntGraphType> { Name = $"{columnName}_gte" });
                    queryArguments.Add(new QueryArgument<IntGraphType> { Name = $"{columnName}_lt" });
                    queryArguments.Add(new QueryArgument<IntGraphType> { Name = $"{columnName}_lte" });
                    queryArguments.Add(new QueryArgument<StringGraphType> { Name = $"{columnName}_iext" });
                    queryArguments.Add(new QueryArgument<StringGraphType> { Name = $"{columnName}_iext_or" });
                    queryArguments.Add(new QueryArgument<BooleanGraphType> { Name = $"{columnName}_isnull" });
                }
                else if (type != typeof(bool))
                {
                    queryArguments.Add(new QueryArgument<StringGraphType> { Name = $"{columnName}_iext" });
                    queryArguments.Add(new QueryArgument<StringGraphType> { Name = $"{columnName}_iext_or" });
                    queryArguments.Add(new QueryArgument<BooleanGraphType> { Name = $"{columnName}_isnull" });
                }
            }

        }

    }

    public class TimeSpanResolver : IFieldResolver
    {
        private string _nameField;

        public TimeSpanResolver(string nameField)
        {
            _nameField = nameField;
        }

        public object Resolve(IResolveFieldContext context)
        {
            var value = context.Source.GetPropertyValue(_nameField);
            if (value == null) return null;
            return ((TimeSpan)value).ToString();
        }

    }
}
