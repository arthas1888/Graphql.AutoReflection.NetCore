using GraphQL;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Graphql.AutoReflection.NetCore.Custom
{
    public class CustomGraphQLRequest
    {
        public const string OPERATION_NAME_KEY = "operationName";
        public const string QUERY_KEY = "query";
        public const string VARIABLES_KEY = "variables";

        public Inputs Inputs { get; set; }

        [JsonPropertyName(QUERY_KEY)]
        public string Query { get; set; }

        [JsonPropertyName(OPERATION_NAME_KEY)]
        public string OperationName { get; set; }

        /// <remarks>
        /// Population of this property during deserialization from JSON requires
        /// <see cref="GraphQL.SystemTextJson.ObjectDictionaryConverter"/>.
        /// </remarks>
        [JsonPropertyName(VARIABLES_KEY)]
        public Dictionary<string, object> Variables { get; set; }
    }
}
