using GraphQL;
using GraphQL.Authorization;
using GraphQL.Language.AST;
using GraphQL.Types;
using GraphQL.Validation;
using Microsoft.AspNetCore.Http;
using SER.Graphql.Reflection.NetCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore.Permissions
{
    public class CustomAuthorizationValidationRule : IValidationRule
    {
        private readonly IAuthorizationEvaluator _evaluator;
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Creates an instance of <see cref="AuthorizationValidationRule"/> with
        /// the specified authorization evaluator.
        /// </summary>
        public CustomAuthorizationValidationRule(IAuthorizationEvaluator evaluator, IHttpContextAccessor httpContextAccessor)
        {
            _evaluator = evaluator;
            _httpContextAccessor = httpContextAccessor;
        }


        /// <inheritdoc />
        public Task<INodeVisitor> ValidateAsync(ValidationContext context)
        {
            var userContext = context.UserContext as IProvideClaimsPrincipal;
            var operationType = OperationType.Query;

            // this could leak info about hidden fields or types in error messages
            // it would be better to implement a filter on the Schema so it
            // acts as if they just don't exist vs. an auth denied error
            // - filtering the Schema is not currently supported
            // TODO: apply ISchemaFilter - context.Schema.Filter.AllowXXX


            return Task.FromResult((INodeVisitor)new NodeVisitors(
                new MatchingNodeVisitor<Operation>((astType, context) =>
                {
                    operationType = astType.OperationType;

                    var type = context.TypeInfo.GetLastType();
                    CheckAuth(astType, type, userContext?.User ?? _httpContextAccessor.HttpContext.User, context, operationType);
                }),

                new MatchingNodeVisitor<ObjectField>((objectFieldAst, context) =>
                {
                    if (context.TypeInfo.GetArgument()?.ResolvedType.GetNamedType() is IComplexGraphType argumentType)
                    {
                        var fieldType = argumentType.GetField(objectFieldAst.Name);
                        CheckAuth(objectFieldAst, fieldType, userContext?.User ?? _httpContextAccessor.HttpContext.User, context, operationType);
                    }
                }),

                new MatchingNodeVisitor<Field>((fieldAst, context) =>
                {
                    var fieldDef = context.TypeInfo.GetFieldDef();

                    if (fieldDef == null)
                        return;

                    // check target field
                    CheckAuth(fieldAst, fieldDef, userContext?.User ?? _httpContextAccessor.HttpContext.User, context, operationType);
                    // check returned graph type
                    CheckAuth(fieldAst, fieldDef.ResolvedType.GetNamedType(), userContext?.User ?? _httpContextAccessor.HttpContext.User, context, operationType);
                })
            ));
        }

        private void CheckAuth(
            INode node,
            IProvideMetadata provider,
            ClaimsPrincipal user,
            ValidationContext context,
            OperationType? operationType)
        {
            if (provider == null || !provider.RequiresAuthorization())
                return;

            // TODO: async -> sync transition
            var result = _evaluator
                .Evaluate(user, context.UserContext, context.Inputs, provider.GetPolicies())
                .GetAwaiter()
                .GetResult();

            if (result.Succeeded)
                return;

            string errors = string.Join("\n", result.Errors);

            context.ReportError(new ValidationError(
                context.Document.OriginalQuery,
                "authorization",
                $"You are not authorized to run this {operationType.ToString().ToLower()}.\n{errors}",
                node));
        }
    }
}
