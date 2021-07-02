using GraphQL;
using GraphQL.Language.AST;
using GraphQL.Resolvers;
using GraphQL.Types;
using GraphQL.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SER.Graphql.Reflection.NetCore.Builder;
using System.Text.Json;

namespace SER.Graphql.Reflection.NetCore.Generic
{
    public class MyFieldResolver<TUser, TRole, TUserRole> : IFieldResolver
        where TUser : class
        where TRole : class
        where TUserRole : class
    {
        private TableMetadata _tableMetadata;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly FillDataExtensions _fillDataExtensions;
        private readonly ILogger _logger;

        public MyFieldResolver(TableMetadata tableMetadata,
            FillDataExtensions fillDataExtensions,
            IHttpContextAccessor httpContextAccessor)
        {
            _tableMetadata = tableMetadata;
            _httpContextAccessor = httpContextAccessor;
            _fillDataExtensions = fillDataExtensions;
            _logger = _httpContextAccessor.HttpContext.RequestServices.GetService<ILogger<MyFieldResolver<TUser, TRole, TUserRole>>>();
        }

        public object Resolve(IResolveFieldContext context)
        {
            var type = _tableMetadata.Type;
            Type graphRepositoryType = typeof(IGraphRepository<>).MakeGenericType(new Type[] { type });
            dynamic service = _httpContextAccessor.HttpContext.RequestServices.GetService(graphRepositoryType);
            var alias = string.IsNullOrEmpty(context.FieldAst.Alias) ? context.FieldAst.Name : context.FieldAst.Alias;
            var whereArgs = new StringBuilder();
            var args = new List<object>();
            var includes = new List<string>();

            //Console.WriteLine($" ----------------------- alias {alias}");
            try
            {
                //var listFieldType = ((dynamic)context.FieldDefinition.ResolvedType).ResolvedType.Fields;

                if (alias.Contains("_list"))
                {
                    GraphUtils.DetectChild<TUser, TRole, TUserRole>(context.FieldAst.SelectionSet.Selections, includes,
                        ((dynamic)context.FieldDefinition.ResolvedType).ResolvedType, args, whereArgs,
                        arguments: context.Arguments, mainType: _tableMetadata.Type);
                    Console.WriteLine($"whereArgs: {whereArgs}");

                    return service
                        .GetAllAsync(alias, whereArgs: whereArgs.ToString(),
                            take: context.GetArgument<int?>("first"), offset: context.GetArgument<int?>("page"),
                            orderBy: context.GetArgument<string>("orderBy"),
                            includeExpressions: includes, args: args.ToArray())
                        .Result;
                }
                else if (alias.Contains("_count"))
                {
                    GraphUtils.DetectChild<TUser, TRole, TUserRole>(context.FieldAst.SelectionSet.Selections, includes,
                        context.FieldDefinition.ResolvedType, args, whereArgs,
                        arguments: context.Arguments, mainType: _tableMetadata.Type);
                    Console.WriteLine($"whereArgs: {whereArgs}");

                    return service.GetCountQuery(whereArgs: whereArgs.ToString(),
                        includeExpressions: includes, args: args.ToArray());
                }
                else if (alias.Contains("_sum"))
                {
                    GraphUtils.DetectChild<TUser, TRole, TUserRole>(context.FieldAst.SelectionSet.Selections, includes,
                        context.FieldDefinition.ResolvedType, args, whereArgs,
                        arguments: context.Arguments, mainType: _tableMetadata.Type);
                    string param = "";
                    Console.WriteLine($"whereArgs: {whereArgs}");
                    if (context.FieldAst.SelectionSet.Selections != null)
                    {
                        foreach (Field field in context.FieldAst.SelectionSet.Selections)
                        {
                            //Console.WriteLine($"name {field.Name}");
                            param = field.Name;
                            context.FieldAst.SelectionSet.Selections.Remove(field);
                            break;
                        }
                    }

                    if (param == null)
                    {
                        GetError(context);
                        return null;
                    }

                    return service.GetSumQuery(param: param, whereArgs: whereArgs.ToString(), includeExpressions: includes, args: args.ToArray());
                }
                else
                {
                    var id = context.GetArgument<dynamic>("id");
                    GraphUtils.DetectChild<TUser, TRole, TUserRole>(context.FieldAst.SelectionSet.Selections, includes,
                        context.FieldDefinition.ResolvedType, args, whereArgs,
                        arguments: context.Arguments, mainType: _tableMetadata.Type);
                    Console.WriteLine($"whereArgs: {whereArgs}");

                    var dbEntity = service
                        .GetByIdAsync(alias, id, whereArgs: whereArgs.ToString(),
                            includeExpressions: includes, args: args.ToArray())
                        .Result;

                    if (dbEntity == null)
                    {
                        GetError(context);
                        return null;
                    }
                    return dbEntity;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
                var error = new ExecutionError(e.Message, e);
                context.Errors.Add(error);
                return null;
            }

        }

        private void GetError(IResolveFieldContext context)
        {
            var error = new ValidationError(context.Document.OriginalQuery,
                "not-found",
                "Couldn't find entity in db.",
                new INode[] { context.FieldAst });
            context.Errors.Add(error);
        }

        public IQueryable GetQueryable(Type type) => GetType()
                .GetMethod("GetListHelper")
                .MakeGenericMethod(type)
                .Invoke(this, null) as IQueryable;

    }
}