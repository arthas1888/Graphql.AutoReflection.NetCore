using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Graphql.Reflection.NetCore
{
    public class AdditionalPermission
    {
        public string Name { get; set; }
        public string[] Permissions { get; set; }
    }
}
