using GraphQL.DataLoader;
using GraphQL.Server.Transports.AspNetCore;
using GraphQL.Types;
using GraphQL.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Graphql.Reflection.NetCore.Custom
{
    public static class GraphConfigureService
    {

        public static void UseGraphQLWithAuth(this IApplicationBuilder app)
        {
            var settings = new GraphQLSettings
            {
                BuildUserContext = ctx =>
                {
                    var userContext = new GraphQLUserContext
                    {
                        User = ctx.User
                    };
                    return userContext;
                }
            };

            var rules = app.ApplicationServices.GetServices<IValidationRule>();
            settings.ValidationRules.AddRange(rules);

            app.UseMiddleware<GraphQLHttpMiddleware<ISchema>>(new PathString("/api/graphql/v1"), settings);
        }
    }

}
