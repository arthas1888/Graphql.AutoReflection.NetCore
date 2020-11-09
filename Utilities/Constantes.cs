using System;
using System.Collections.Generic;
using System.Text;

namespace Graphql.AutoReflection.NetCore.Utilities
{
    public class Constantes
    {
        public const string SuperUser = "Super-User";

        public static string[] SystemTables = new string[]
       {
            "AspNetRoles", "AspNetUsers", "AspNetUserRoles",
            "AspNetRoleClaims", "AspNetUserClaims", "AspNetUserLogins", "AspNetUserTokens"
       };

        public static string[] SystemTablesSnakeCase = new string[]
        {
            "asp_net_role_claims", "asp_net_user_claims", "asp_net_user_logins", "asp_net_user_tokens"
        };
        // "asp_net_roles",  "asp_net_users",  "asp_net_user_roles",  

        public static string[] SystemTablesSingular = new string[]
        {
             "ApplicationRole", "ApplicationUser", "ApplicationUserRole"
        };
    }

    public class CustomClaimTypes
    {
        public const string Permission = "http://ngcore/claims/permission";
        public const string CompanyId = "http://ngcore/claims/owner_id";
    }
}
