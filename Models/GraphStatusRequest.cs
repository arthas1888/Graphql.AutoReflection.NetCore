using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore.Models
{
    public class GraphStatusRequest
    {
        public string ClassName { get; set; }
        public int Action { get; set; }
        public string Id { get; set; }
        public string CompanyId { get; set; }
    }
}
