using SER.Graphql.Reflection.NetCore.Generic;
using GraphQL.Types;
using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using SER.Graphql.Reflection.NetCore.Utilities;
using Microsoft.Extensions.Options;
using SER.Graphql.Reflection.NetCore.Builder;
using SER.Models;
using SER.Graphql.Reflection.NetCore.Models;
using SER.Graphql.Reflection.NetCore.CustomScalar;

namespace SER.Graphql.Reflection.NetCore
{
    public class SumTableType<T> : ObjectGraphType<SumObjectResponse<T>> where T : class
    {
        private ITableNameLookup _tableNameLookup;
        private IDatabaseMetadata _dbMetadata;
        private readonly IOptionsMonitor<SERGraphQlOptions> _optionsDelegate;
        public QueryArguments TableArgs { get; set; }

        public SumTableType(
            IDatabaseMetadata dbMetadata,
            TableMetadata mainTable,
            ITableNameLookup tableNameLookup,
            IOptionsMonitor<SERGraphQlOptions> optionsDelegate)
        {
            _tableNameLookup = tableNameLookup;
            _dbMetadata = dbMetadata;
            _optionsDelegate = optionsDelegate;

            var permission = mainTable.Type.Name.ToLower();
            var friendlyTableName = _tableNameLookup.GetFriendlyName(mainTable.Type.Name.ToSnakeCase());
            this.ValidatePermissions(permission, friendlyTableName, mainTable.Type, _optionsDelegate);
            // this.RequireAuthentication(); 

            Name = mainTable.TableName + "_sum";

            Field(
                typeof(DecimalGraphType),
                "response_sum"
            );

            foreach (var mainTableColumn in mainTable.Columns)
            {
                InitMainGraphTableColumn(mainTableColumn);
            }
        }

        private void InitMainGraphTableColumn(ColumnMetadata mainTableColumn)
        {
            if (mainTableColumn.IsList)
            {
                GetInternalInstances(mainTableColumn, isList: true);
            }
            else if (typeof(IBaseModel).IsAssignableFrom(mainTableColumn.Type)
                || _dbMetadata.GetTableMetadatas().Any(x => x.Type == mainTableColumn.Type))
            {
                GetInternalInstances(mainTableColumn);
            }
            else
            {
                if (Utilities.TypeExtensions.IsNumber(mainTableColumn.Type))
                    Field(
                       GraphUtils.ResolveGraphType(mainTableColumn.Type),
                       mainTableColumn.ColumnName
                    );

                FillArgs(mainTableColumn.ColumnName, mainTableColumn.Type);
                if (mainTableColumn.Type.IsEnum)
                {
                    FillArgs($"{mainTableColumn.ColumnName}_enum", typeof(int));
                }
            }
        }

        private void GetInternalInstances(ColumnMetadata columnMetadata, bool isList = false)
        {
            var parentTypeName = columnMetadata.Type.Name;
            var metaTable = _dbMetadata.GetTableMetadatas().FirstOrDefault(x => x.Type.Name == parentTypeName);
            foreach (var tableColumn in metaTable.Columns)
            {
                if (tableColumn.IsList || typeof(IBaseModel).IsAssignableFrom(tableColumn.Type)
                    || _dbMetadata.GetTableMetadatas().Any(x => x.Type == tableColumn.Type))
                {
                }
                else
                {
                    FillArgs(tableColumn.ColumnName, tableColumn.Type, parentModel: columnMetadata.ColumnName, isList: isList);
                }
            }
        }

        private void FillArgs(string columnName, Type type, string parentModel = "", bool isList = false)
        {
            if (!string.IsNullOrEmpty(parentModel))
                if (isList) columnName = $"{parentModel}__list__{columnName}";
                else columnName = $"{parentModel}__model__{columnName}";

            if (TableArgs == null)
            {
                TableArgs = new QueryArguments
                {
                    new QueryArgument<StringGraphType> { Name = "all" },
                    //new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "param" }
                };
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
                TableArgs.Add(new QueryArgument<IdGraphType> { Name = $"{columnName}_exclude" });
            }
            else
            {
                var queryArgument = new QueryArgument(GraphUtils.ResolveGraphType(type)) { Name = columnName };
                TableArgs.Add(queryArgument);
                TableArgs.Add(new QueryArgument(GraphUtils.ResolveGraphType(type)) { Name = $"{columnName}_exclude" });

                if (type == typeof(DateTime?) || type == typeof(DateTime))
                {
                    TableArgs.Add(new QueryArgument<MyDateTimeGraphType> { Name = $"{columnName}_gt" });
                    TableArgs.Add(new QueryArgument<MyDateTimeGraphType> { Name = $"{columnName}_gte" });
                    TableArgs.Add(new QueryArgument<MyDateTimeGraphType> { Name = $"{columnName}_lt" });
                    TableArgs.Add(new QueryArgument<MyDateTimeGraphType> { Name = $"{columnName}_lte" });
                }
                else if (type == typeof(int?) || type == typeof(int) || type == typeof(decimal?) || type == typeof(decimal)
                    || type == typeof(double?) || type == typeof(double) || type == typeof(float?) || type == typeof(float))
                {
                    TableArgs.Add(new QueryArgument<MyIntGraphType> { Name = $"{columnName}_gt" });
                    TableArgs.Add(new QueryArgument<MyIntGraphType> { Name = $"{columnName}_gte" });
                    TableArgs.Add(new QueryArgument<MyIntGraphType> { Name = $"{columnName}_lt" });
                    TableArgs.Add(new QueryArgument<MyIntGraphType> { Name = $"{columnName}_lte" });
                    TableArgs.Add(new QueryArgument<StringGraphType> { Name = $"{columnName}_iext" });
                    TableArgs.Add(new QueryArgument<StringGraphType> { Name = $"{columnName}_iext_or" });
                    TableArgs.Add(new QueryArgument<BooleanGraphType> { Name = $"{columnName}_isnull" });
                }
                else if (type == typeof(ulong?) || type == typeof(ulong) || type == typeof(long?) || type == typeof(long))
                {
                    TableArgs.Add(new QueryArgument<MyLongGraphType> { Name = $"{columnName}_gt" });
                    TableArgs.Add(new QueryArgument<MyLongGraphType> { Name = $"{columnName}_gte" });
                    TableArgs.Add(new QueryArgument<MyLongGraphType> { Name = $"{columnName}_lt" });
                    TableArgs.Add(new QueryArgument<MyLongGraphType> { Name = $"{columnName}_lte" });
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
    }
}
