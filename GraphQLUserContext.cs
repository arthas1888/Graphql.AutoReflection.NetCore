using GraphQL.Authorization;
using GraphQL.Server.Transports.AspNetCore;
using GraphQL.Validation;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore
{
    public class GraphQLUserContext : Dictionary<string, object>
    {
        public ClaimsPrincipal User { get; set; }

        public GraphQLUserContext(ClaimsPrincipal user)
        {
            User = user;
        }

    }
}
