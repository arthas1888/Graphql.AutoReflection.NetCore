using System;
using System.Collections.Generic;
using System.Text;

namespace SER.Graphql.Reflection.NetCore.Utilities
{
    public class Constants
    {
        public static string[] SystemTablesSnakeCase = new string[]
        {
            "asp_net_role_claims", "asp_net_user_claims", "asp_net_user_logins", "asp_net_user_tokens"
        };
    }

    public class GraphClaimTypes
    {
        public const string Permission = "http://ngcore/claims/permission";
        public const string CompanyId = "company_id";
    }
}
