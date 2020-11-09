using System;
using System.Collections.Generic;
using System.Text;

namespace SER.Graphql.Reflection.NetCore.Builder
{
    /// <summary>
    /// Provides various settings needed to configure
    /// the Graphql auto reflection integration.
    /// </summary>
    public class GraphQLOptions
    {
        /// <summary>
        /// Gets or sets the concrete type of the <see cref="DbContext"/> used by the
        /// Graphql auto reflection stores. If this property is not populated,
        /// an exception is thrown at runtime when trying to use the stores.
        /// </summary>
        public Type DbContextType { get; set; }
    }
}
