using SER.Graphql.Reflection.NetCore.Generic;
using SER.Graphql.Reflection.NetCore.Models;
using GraphQL.Types;
using Microsoft.AspNetCore.Http;
using System;
using SER.Graphql.Reflection.NetCore.Utilities;
using System.Linq;
using GraphQL.Authorization;

namespace SER.Graphql.Reflection.NetCore
{
    public class AppMutation : ObjectGraphType<object>
    {
        private IDatabaseMetadata _dbMetadata;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private ITableNameLookup _tableNameLookup;

        public AppMutation(
            IDatabaseMetadata dbMetadata,
             ITableNameLookup tableNameLookup,
            IHttpContextAccessor httpContextAccessor
            )
        {
            _dbMetadata = dbMetadata;
            _httpContextAccessor = httpContextAccessor;
            _tableNameLookup = tableNameLookup;

            this.AuthorizeWith("Authorized");

            foreach (var metaTable in _dbMetadata.GetTableMetadatas())
            {
                if (Constantes.SystemTablesSingular.Contains(metaTable.Type.Name)) continue;
                var type = metaTable.Type;
                var friendlyTableName = type.Name.ToLower().ToSnakeCase();

                var genericInputType = new GenericInputType(metaTable, _dbMetadata, _tableNameLookup);
                dynamic objectGraphType = null;
                if (!_tableNameLookup.ExistGraphType(metaTable.Type.Name))
                {
                    var inherateType = typeof(TableType<>).MakeGenericType(new Type[] { metaTable.Type });
                    objectGraphType = Activator.CreateInstance(inherateType, new object[] { metaTable,
                        _dbMetadata, _tableNameLookup, _httpContextAccessor, false });
                }

                var tableType = _tableNameLookup.GetOrInsertGraphType(metaTable.Type.Name, objectGraphType);

                AddField(new FieldType
                {
                    Name = $"create_{friendlyTableName}",
                    Type = tableType.GetType(),
                    ResolvedType = tableType,
                    Resolver = new CUDResolver(type, _httpContextAccessor),
                    Arguments = new QueryArguments(
                        new QueryArgument(typeof(InputObjectGraphType)) { Name = friendlyTableName, ResolvedType = genericInputType }
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
                        new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "id" }
                    )
                });

                AddField(new FieldType
                {
                    Name = $"delete_{friendlyTableName}",
                    Type = tableType.GetType(),
                    ResolvedType = tableType,
                    Arguments = new QueryArguments(new QueryArgument<NonNullGraphType<IdGraphType>> { Name = $"{friendlyTableName}Id" }),
                    Resolver = new CUDResolver(type, _httpContextAccessor)
                });
            }
        }
    }
}