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

namespace SER.Graphql.Reflection.NetCore.Custom
{
    public static class GraphConfigureService
    {

        public static void UseGraphQLWithAuth(this IApplicationBuilder app)
        {
            app.UseMiddleware<GraphQLHttpMiddleware<ISchema>>(new PathString("/api/graphql/v1"));
        }
    }

}
