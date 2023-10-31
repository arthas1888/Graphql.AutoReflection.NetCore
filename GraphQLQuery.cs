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
using SER.Graphql.Reflection.NetCore.Models;
using GraphQL.Resolvers;
using GraphQL;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore.Generic
{
    public class GraphQLQuery<TUser, TRole, TUserRole> : ObjectGraphType<object>
        where TUser : class
        where TRole : class
        where TUserRole : class
    {
        private readonly IDatabaseMetadata _dbMetadata;
        private readonly ITableNameLookup _tableNameLookup;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly FillDataExtensions _fillDataExtensions;
        private readonly IDataLoaderContextAccessor _accessor;
        private readonly ISERFieldResolver<TUser, TRole, TUserRole> _fieldResolver;
        private readonly IOptionsMonitor<SERGraphQlOptions> _optionsDelegate;

        public GraphQLQuery(
            IDatabaseMetadata dbMetadata,
            ITableNameLookup tableNameLookup,
            IHttpContextAccessor httpContextAccessor,
            FillDataExtensions fillDataExtensions,
            IDataLoaderContextAccessor accessor,
            ISERFieldResolver<TUser, TRole, TUserRole> fieldResolver,
            IOptionsMonitor<SERGraphQlOptions> optionsDelegate
            )
        {
            _dbMetadata = dbMetadata;
            _tableNameLookup = tableNameLookup;
            _httpContextAccessor = httpContextAccessor;
            _fillDataExtensions = fillDataExtensions;
            _accessor = accessor;
            _optionsDelegate = optionsDelegate;
            _fieldResolver = fieldResolver;

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

                dynamic objectSumGraphType = null;
                if (!_tableNameLookup.ExistGraphType($"{metaTable.Type.Name}_sum_plus"))
                {
                    var inherateType = typeof(SumTableType<>).MakeGenericType(new Type[] { metaTable.Type });
                    objectSumGraphType = Activator.CreateInstance(inherateType, new object[] { _dbMetadata, metaTable, _tableNameLookup, _optionsDelegate });
                }
                var sumTableType = _tableNameLookup.GetOrInsertGraphType($"{metaTable.Type.Name}_sum_plus", objectSumGraphType);
                var ttype = typeof(TableType<>).MakeGenericType(new Type[] { metaTable.Type });

                AddField(new FieldType
                {
                    Name = friendlyTableName,
                    Type = tableType.GetType(),
                    ResolvedType = tableType,
                    Resolver = _fieldResolver, // new MyFieldResolver<TUser, TRole, TUserRole>(_fillDataExtensions, _httpContextAccessor),
                    Arguments = new QueryArguments(tableType.TableArgs)
                });

                var inherateListType = typeof(ListGraphType<>).MakeGenericType(new Type[] { tableType.GetType() });
                dynamic listType = Activator.CreateInstance(inherateListType);
                listType.ResolvedType = tableType;

                AddField(new FieldType
                {
                    Name = $"{friendlyTableName}_list",
                    Type = listType.GetType(),
                    ResolvedType = listType,
                    Resolver = _fieldResolver,
                    Arguments = new QueryArguments(tableType.TableArgs)
                });

                AddField(new FieldType
                {
                    Name = $"{friendlyTableName}_count",
                    Type = countTableType.GetType(),
                    ResolvedType = countTableType,
                    Resolver = _fieldResolver,
                    Arguments = new QueryArguments(countTableType.TableArgs)
                });

                AddField(new FieldType
                {
                    Name = $"{friendlyTableName}_sum",
                    Type = ttype,
                    ResolvedType = sumTableType,
                    Resolver = _fieldResolver,
                    Arguments = new QueryArguments(sumTableType.TableArgs)
                });
            }
        }
    }
}
