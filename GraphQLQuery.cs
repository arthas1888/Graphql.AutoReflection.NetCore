using GraphQL.Types;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System;
using System.Reflection;
using SER.Graphql.Reflection.NetCore.Utilities;
using GraphQL.DataLoader;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SER.Graphql.Reflection.NetCore.Builder;

namespace SER.Graphql.Reflection.NetCore.Generic
{
    public class GraphQLQuery<TUser, TRole, TUserRole> : ObjectGraphType<object>
        where TUser : class
        where TRole : class
        where TUserRole : class
    {
        private IDatabaseMetadata _dbMetadata;
        private ITableNameLookup _tableNameLookup;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly FillDataExtensions _fillDataExtensions;
        private readonly IDataLoaderContextAccessor _accessor;
        private readonly IOptionsMonitor<SERGraphQlOptions> _optionsDelegate;
        public GraphQLQuery(
            IDatabaseMetadata dbMetadata,
            ITableNameLookup tableNameLookup,
            IHttpContextAccessor httpContextAccessor,
            FillDataExtensions fillDataExtensions,
            IDataLoaderContextAccessor accessor,
            IOptionsMonitor<SERGraphQlOptions> optionsDelegate
            )
        {
            _dbMetadata = dbMetadata;
            _tableNameLookup = tableNameLookup;
            _httpContextAccessor = httpContextAccessor;
            _fillDataExtensions = fillDataExtensions;
            _accessor = accessor;
            _optionsDelegate = optionsDelegate;

            Name = "Query";
            var tables = _dbMetadata.GetTableMetadatas();

            foreach (var metaTable in tables)
            {
                var friendlyTableName = metaTable.Type.Name.ToSnakeCase().ToLower();

                dynamic objectGraphType = null;
                if (!_tableNameLookup.ExistGraphType(metaTable.Type.Name))
                {
                    var inherateType = typeof(TableType<>).MakeGenericType(new Type[] { metaTable.Type });
                    objectGraphType = Activator.CreateInstance(inherateType, new object[] { metaTable,
                        _dbMetadata, _tableNameLookup, _httpContextAccessor, _accessor, _optionsDelegate });
                }

                var tableType = _tableNameLookup.GetOrInsertGraphType(metaTable.Type.Name, objectGraphType);

                dynamic objectCountGraphType = null;
                if (!_tableNameLookup.ExistGraphType($"{metaTable.Type.Name}_count"))
                {
                    var inherateType = typeof(CountTableType<>).MakeGenericType(new Type[] { metaTable.Type });
                    objectCountGraphType = Activator.CreateInstance(inherateType, new object[] { _dbMetadata, metaTable, _tableNameLookup, _optionsDelegate });
                }

                var countTableType = _tableNameLookup.GetOrInsertGraphType($"{metaTable.Type.Name}_count", objectCountGraphType);

                AddField(new FieldType
                {
                    Name = friendlyTableName,
                    Type = tableType.GetType(),
                    ResolvedType = tableType,
                    Resolver = new MyFieldResolver<TUser, TRole, TUserRole>(metaTable, _fillDataExtensions, _httpContextAccessor),
                    Arguments = new QueryArguments(tableType.TableArgs)
                });

                var listType = new ListGraphType<ObjectGraphType<dynamic>>();
                listType.ResolvedType = tableType;

                AddField(new FieldType
                {
                    Name = $"{friendlyTableName}_list",
                    Type = listType.GetType(),
                    ResolvedType = listType,
                    Resolver = new MyFieldResolver<TUser, TRole, TUserRole>(metaTable, _fillDataExtensions, _httpContextAccessor),
                    Arguments = new QueryArguments(tableType.TableArgs)
                });

                AddField(new FieldType
                {
                    Name = $"{friendlyTableName}_count",
                    Type = countTableType.GetType(),
                    ResolvedType = countTableType,
                    Resolver = new MyFieldResolver<TUser, TRole, TUserRole>(metaTable, _fillDataExtensions, _httpContextAccessor),
                    Arguments = new QueryArguments(countTableType.TableArgs)
                });
            }
        }
    }

}
