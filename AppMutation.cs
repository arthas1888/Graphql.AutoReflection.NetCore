using SER.Graphql.Reflection.NetCore.Generic;
using GraphQL.Types;
using Microsoft.AspNetCore.Http;
using System;
using SER.Graphql.Reflection.NetCore.Utilities;
using Microsoft.Extensions.Options;
using SER.Graphql.Reflection.NetCore.Builder;
using GraphQL.DataLoader;
using GraphQL;

namespace SER.Graphql.Reflection.NetCore
{
    public class AppMutation : ObjectGraphType<object>
    {
        private IDatabaseMetadata _dbMetadata;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private ITableNameLookup _tableNameLookup;
        private readonly IOptionsMonitor<SERGraphQlOptions> _optionsDelegate;
        private readonly IDataLoaderContextAccessor _accessor;

        public AppMutation(
            IDatabaseMetadata dbMetadata,
            ITableNameLookup tableNameLookup,
            IHttpContextAccessor httpContextAccessor,
            IOptionsMonitor<SERGraphQlOptions> optionsDelegate,
            IDataLoaderContextAccessor accessor
            )
        {
            _dbMetadata = dbMetadata;
            _httpContextAccessor = httpContextAccessor;
            _tableNameLookup = tableNameLookup;
            _optionsDelegate = optionsDelegate;
            _accessor = accessor;

            this.RequireAuthentication();
            Name = "Mutation";

            foreach (var metaTable in _dbMetadata.GetTableMetadatas())
            {
                if (metaTable.Type == _optionsDelegate.CurrentValue.UserType
                     || metaTable.Type == _optionsDelegate.CurrentValue.RoleType
                     || metaTable.Type == _optionsDelegate.CurrentValue.UserRoleType) continue;

                var type = metaTable.Type;
                var friendlyTableName = type.Name.ToLower().ToSnakeCase();

                var genericInputType = new GenericInputType(metaTable, _dbMetadata, _tableNameLookup, _optionsDelegate);
                dynamic objectGraphType = null;
                if (!_tableNameLookup.ExistGraphType(metaTable.Type.Name))
                {
                    var inherateType = typeof(TableType<>).MakeGenericType(new Type[] { metaTable.Type });
                    objectGraphType = Activator.CreateInstance(inherateType, new object[] { metaTable,
                        _dbMetadata, _tableNameLookup, _httpContextAccessor, _accessor, _optionsDelegate });
                }

                var tableType = _tableNameLookup.GetOrInsertGraphType(metaTable.Type.Name, objectGraphType);

                AddField(new FieldType
                {
                    Name = $"create_{friendlyTableName}",
                    Type = tableType.GetType(),
                    ResolvedType = tableType,
                    Resolver = new CUDResolver(type, _httpContextAccessor),
                    Arguments = new QueryArguments(
                        new QueryArgument(typeof(InputObjectGraphType)) { Name = friendlyTableName, ResolvedType = genericInputType },
                        new QueryArgument<BooleanGraphType> { Name = "sendObjFirebase" }
                    ),
                });

                AddField(new FieldType
                {
                    Name = $"update_{friendlyTableName}",
                    Type = tableType.GetType(),
                    ResolvedType = tableType,
                    Resolver = new CUDResolver(type, _httpContextAccessor),
                    Arguments = new QueryArguments(
                        new QueryArgument(typeof(InputObjectGraphType)) { Name = friendlyTableName, ResolvedType = genericInputType },
                        new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "id" },
                        new QueryArgument<BooleanGraphType> { Name = "sendObjFirebase" }
                    )
                });

                AddField(new FieldType
                {
                    Name = $"delete_{friendlyTableName}",
                    Type = tableType.GetType(),
                    ResolvedType = tableType,
                    Arguments = new QueryArguments(
                        new QueryArgument<NonNullGraphType<IdGraphType>> { Name = $"{friendlyTableName}Id" },
                        new QueryArgument<BooleanGraphType> { Name = "sendObjFirebase" }),
                    Resolver = new CUDResolver(type, _httpContextAccessor)
                });
            }
        }
    }
}