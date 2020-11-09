using GraphQL.Validation;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore.Custom
{
    //public class GraphQLSettings
    //{
    //    public Func<HttpContext, Task<object>> BuildUserContext { get; set; }
    //    public object Root { get; set; }
    //    public List<IValidationRule> ValidationRules { get; } = new List<IValidationRule>();
    //}

    public class GraphQLSettings
    {
        public PathString Path { get; set; } = "/api/graphql/v1";

        public Func<HttpContext, IDictionary<string, object>> BuildUserContext { get; set; }

        public bool EnableMetrics { get; set; } = true;

        public List<IValidationRule> ValidationRules { get; } = new List<IValidationRule>();
    }
}
