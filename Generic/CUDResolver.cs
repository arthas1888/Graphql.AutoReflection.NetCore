using GraphQL;
using GraphQL.Resolvers;
using GraphQL.Types;
using GraphQL.Validation;
using Microsoft.AspNetCore.Http;
using System;
using SER.Graphql.Reflection.NetCore.Utilities;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using GraphQLParser.AST;

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

        public object Resolve(IResolveFieldContext context)
        {
            //Console.WriteLine($"----------------------Alias: {_type} {context.FieldAst.Alias} NAME {context.FieldAst.Name} ");

            dynamic entity = context.GetArgument(_type, _type.Name.ToLower().ToSnakeCase(), defaultValue: null);

            Type graphRepositoryType = typeof(IGraphRepository<>).MakeGenericType(new Type[] { _type });
            dynamic service = _httpContextAccessor.HttpContext.RequestServices.GetService(graphRepositoryType);
            dynamic id = null;
            var sendObjFirebase = context.GetArgument<bool?>("sendObjFirebase") ?? true;
            dynamic deleteId = null;
            if (context.HasArgument("id"))
            {
                id = context.GetArgument<dynamic>("id");
                if (id is int) id = (int)id;
                else if (id is int) id = id.ToString();
                else if (id is Guid) id = (Guid)deleteId;

                Console.WriteLine($"----------------------id: {id}  {id.GetType()} ----------------------");
            }
            if (context.HasArgument($"{_type.Name.ToLower().ToSnakeCase()}Id"))
            {
                deleteId = context.GetArgument<object>($"{_type.Name.ToLower().ToSnakeCase()}Id");
                if (deleteId is int) deleteId = (int)deleteId;
                else if (deleteId is int) deleteId = deleteId.ToString();
                else if (deleteId is Guid) deleteId = (Guid)deleteId;
            }


            var alias = string.IsNullOrEmpty(context.FieldAst.Alias?.Name?.StringValue) ? context.FieldAst.Name.StringValue : context.FieldAst.Alias.Name.StringValue;
            var mainType = _type;

            string model = "";
            FieldType fieldType = null;
            List<string> includes = new();
            dynamic resolvedType = context.FieldDefinition.ResolvedType;

            foreach (var field in context.FieldAst.SelectionSet.Selections.Where(x => x is GraphQLField)
                            .Select(x => x as GraphQLField).ToList())
            {
                if (field.SelectionSet.Selections.Count > 0)
                {
                    model = field.Name.StringValue;
                    try
                    {
                        fieldType = ((IEnumerable<FieldType>)resolvedType.Fields).SingleOrDefault(x => x.Name == field.Name);
                    }
                    catch (Exception) { }
                    if (fieldType != null)
                    {
                        // detect if field is object
                        if (fieldType.ResolvedType.GetType().IsGenericType && fieldType.ResolvedType is not ListGraphType
                            && fieldType.ResolvedType.GetType().GetGenericTypeDefinition() == typeof(ObjectGraphType<>))
                        {
                            includes.Add(model);
                        }
                    }
                }
            }


            if (id != null)
            {
                var argName = context.FieldAst.Arguments.FirstOrDefault(x => x.Name == _type.Name.ToLower().ToSnakeCase());
                object dbEntity = null;
                var variable = context.Variables.FirstOrDefault(x => x.Name == (string)argName.Value.GetPropertyValue(typeof(string)));
                if (variable != null && variable.Value.GetType() == typeof(Dictionary<string, object>))
                    dbEntity = service.Update(id, entity, (Dictionary<string, object>)variable.Value, alias, sendObjFirebase, includes);
                else
                    dbEntity = service.Update(id, entity, ((dynamic)argName.Value).Value, alias, sendObjFirebase, includes);

                if (dbEntity == null)
                {
                    GetError(context);
                    return null;
                }
                return dbEntity;
            }

            if (deleteId != null)
            {
                var dbEntity = service.Delete(deleteId, alias, sendObjFirebase);
                if (dbEntity == null)
                {
                    GetError(context);
                    return null;
                }
                return dbEntity;
            }
            //var service = _httpContextAccessor.HttpContext.RequestServices.GetService<IGraphRepository<Permission>>();
            return service.Create(entity, alias, sendObjFirebase, includes);
        }

        public ValueTask<object> ResolveAsync(IResolveFieldContext context) => new(Resolve(context));
        
        private void GetError(IResolveFieldContext context)
        {
            var error = new ValidationError(context.Document.Source,
                "not-found",
                "Couldn't find entity in db.",
                new ASTNode[] { context.FieldAst });
            context.Errors.Add(error);
        }

    }
}
