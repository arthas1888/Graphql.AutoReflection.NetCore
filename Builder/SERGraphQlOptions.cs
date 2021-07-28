using Microsoft.EntityFrameworkCore;
using SER.Models;
using System;

namespace SER.Graphql.Reflection.NetCore.Builder
{
    /// <summary>
    /// Provides various settings needed to configure
    /// the Graphql auto reflection integration.
    /// </summary>
    public class SERGraphQlOptions
    {

        /// <summary>
        /// Gets or sets the concrete type of the <see cref="DbContext"/> used by the
        /// Graphql auto reflection stores. If this property is not populated,
        /// an exception is thrown at runtime when trying to use the stores.
        /// </summary>
        public Type DbContextType { get; set; }
        public Type UserType { get; set; }
        public Type RoleType { get; set; }
        public Type UserRoleType { get; set; }
        public string ConnectionString { get; set; }
        public string SecurityKey { get; set; }
        public string SigningKey { get; set; }
        public string TokenIssuer { get; set; }
        public bool EnableCustomFilter { get; set; }
        public string NameCustomFilter { get; set; }
        public string NameClaimCustomFilter { get; set; }
        public bool EnableStatusMutation { get; set; }
        public bool EnableAudit { get; set; }

        public Action<GraphStatusRequest> CallbackStatus { get; set; }
    }
}
