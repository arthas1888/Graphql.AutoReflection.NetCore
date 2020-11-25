using GraphQL;
using GraphQL.Language.AST;
using GraphQL.Resolvers;
using GraphQL.Types;
using GraphQL.Validation;
using Microsoft.AspNetCore.Http;
using System;
using SER.Graphql.Reflection.NetCore.Utilities;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;

namespace SER.Graphql.Reflection.NetCore.Generic
{
    public class CUDResolver : IFieldResolver
    {
        private Type _type;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public CUDResolver(
            Type type,
            IHttpContextAccessor httpContextAccessor)
        {
            _type = type;
            _httpContextAccessor = httpContextAccessor;
        }

        public dynamic Resolve(IResolveFieldContext context)
        {
            //Console.WriteLine($"----------------------Alias: {_type} {context.FieldAst.Alias} NAME {context.FieldAst.Name} ");

            dynamic entity = context.GetArgument(_type, _type.Name.ToLower().ToSnakeCase(), defaultValue: null);

            Type graphRepositoryType = typeof(IGraphRepository<>).MakeGenericType(new Type[] { _type });
            dynamic service = _httpContextAccessor.HttpContext.RequestServices.GetService(graphRepositoryType);
            var id = context.GetArgument<int?>("id");
            var deleteId = context.GetArgument<int?>($"{_type.Name.ToLower().ToSnakeCase()}Id");
            var alias = string.IsNullOrEmpty(context.FieldAst.Alias) ? context.FieldAst.Name : context.FieldAst.Alias;

            if (id.HasValue)
            {
                var argName = context.FieldAst.Arguments.FirstOrDefault(x => x.Name == _type.Name.ToLower().ToSnakeCase());
                object dbEntity = null;
                var variable = context.Variables.FirstOrDefault(x => x.Name == (string)argName.Value.Value);
                if (variable != null && variable.Value.GetType() == typeof(Dictionary<string, object>))
                    dbEntity = service.Update(id.Value, entity, (Dictionary<string, object>)variable.Value, alias);
                else
                    dbEntity = service.Update(id.Value, entity, (Dictionary<string, object>)argName.Value.Value, alias);

                if (dbEntity == null)
                {
                    GetError(context);
                    return null;
                }
                return dbEntity;
            }

            if (deleteId.HasValue)
            {
                var dbEntity = service.Delete(deleteId.Value, alias);
                if (dbEntity == null)
                {
                    GetError(context);
                    return null;
                }
                return dbEntity;
            }
            //var service = _httpContextAccessor.HttpContext.RequestServices.GetService<IGraphRepository<Permission>>();
            return service.Create(entity, alias);
        }

        private void GetError(IResolveFieldContext context)
        {
            var error = new ValidationError(context.Document.OriginalQuery,
                "not-found",
                "Couldn't find entity in db.",
                new INode[] { context.FieldAst });
            context.Errors.Add(error);
        }
    }
}
