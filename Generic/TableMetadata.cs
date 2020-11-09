using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore.Generic
{
    public class TableMetadata
    {
        public Type Type { get; set; }
        public string TableName { get; set; }
        public string AssemblyFullName { get; set; }
        public IEnumerable<ColumnMetadata> Columns { get; set; }
    }
}
