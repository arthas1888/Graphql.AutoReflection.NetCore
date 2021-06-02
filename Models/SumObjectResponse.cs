using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore.Models
{
    public class SumObjectResponse<T> where T : class
    {
        public object response_sum { get; set; }
        public T obj { get; set; }
    }
}
