using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore
{
    public class AdditionalPermission
    {
        public string Name { get; set; }
        public EspecificPermission Permissions { get; set; }
    }

    public class EspecificPermission
    {
        [JsonPropertyName("view")]
        public string[] View { get; set; }
        [JsonPropertyName("add")]
        public string[] Add { get; set; }
        [JsonPropertyName("update")]
        public string[] Update { get; set; }
        [JsonPropertyName("delete")]
        public string[] Delete { get; set; }
    }
}
