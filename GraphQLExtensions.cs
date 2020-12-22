using GraphQL.Authorization;
using GraphQL.Builders;
using GraphQL.Types;
using Microsoft.Extensions.Options;
using SER.Graphql.Reflection.NetCore.Builder;
using SER.Graphql.Reflection.NetCore.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore
{
    public static class GraphQLExtensions
    {
        public static readonly string PermissionsKey = "Permissions";

        public static List<AdditionalPermission> additionalPermissions = null;

        public static bool RequiresPermissions(this IProvideMetadata type)
        {
            var permissions = type.GetMetadata<IEnumerable<string>>(PermissionsKey, new List<string>());
            return permissions.Any();
        }

        public static IEnumerable<string> GetPermissions(this IProvideMetadata type)
        {
            return type.GetMetadata<IEnumerable<string>>(PermissionsKey, new List<string>());
        }

        public static bool CanAccess(this IProvideMetadata type, IEnumerable<Claim> claims)
        {
            var permissions = type.GetMetadata<IEnumerable<string>>(PermissionsKey, new List<string>());
            return permissions.All(x => claims.Select(i => i.Value)?.Contains(x) ?? false);
        }

        public static bool HasPermission(this IProvideMetadata type, string permission)
        {
            var permissions = type.GetMetadata<IEnumerable<string>>(PermissionsKey, new List<string>());
            return permissions.Any(x => string.Equals(x, permission));
        }

        public static List<AdditionalPermission> GetOtherPermissions()
        {
            if (additionalPermissions == null)
            {
                try
                {
                    var jsonString = File.ReadAllText("permissions.graphql.json");
                    additionalPermissions = JsonSerializer.Deserialize<List<AdditionalPermission>>(jsonString);
                }
                catch (Exception) { }
            }
            return additionalPermissions;
        }

        public static void ValidatePermissions(this IProvideMetadata type, string permission, string friendlyTableName, Type mainType,
            IOptionsMonitor<SERGraphQlOptions> options)
        {
            var typesWithoutAuthentication = new List<string>();
            var typesWithoutPermission = new List<string>();

            try
            {
                var listWithoutAuth = JsonSerializer.Deserialize<List<string>>(File.ReadAllText("permissions.graphql.without-auth.json"));
                typesWithoutAuthentication.AddRange(listWithoutAuth);

                var listWithoutPerm = JsonSerializer.Deserialize<List<string>>(File.ReadAllText("permissions.graphql.without-perm.json"));
                typesWithoutPermission.AddRange(listWithoutPerm);
            }
            catch (Exception)
            {
            }
            if (!typesWithoutAuthentication.Contains(permission) &&
               !typesWithoutAuthentication.Contains(friendlyTableName))
            {
                type.RequireAuthentication();

                if (!typesWithoutPermission.Contains(permission) &&
                    !typesWithoutPermission.Contains(friendlyTableName))
                {
                    var otherPerms = GetOtherPermissions().Where(x => x.Name == permission).SelectMany(x => x.Permissions.View).ToArray();
                    type.RequirePermissions(otherPerms.Select(x => x + "__VIEW__").ToArray());
                    var otherfriendlyPerms = GetOtherPermissions().Where(x => x.Name == friendlyTableName).SelectMany(x => x.Permissions.View).ToArray();
                    type.RequirePermissions(otherfriendlyPerms.Select(x => x + "__VIEW__").ToArray());

                    if (mainType == options.CurrentValue.UserType
                        || mainType == options.CurrentValue.RoleType
                        || mainType == options.CurrentValue.UserRoleType)
                        type.RequirePermissions($"{friendlyTableName}.view");
                    else
                        type.RequirePermissions($"{permission}.view");
                }
            }
        }

        public static void ValidateCUDPermissions(this IProvideMetadata type, string permission)
        {
            var otherPerms = GetOtherPermissions();
            var otherPermsAdd = otherPerms.Where(x => x.Name == permission).SelectMany(x => x.Permissions.Add).ToArray();
            type.RequirePermissions(otherPermsAdd.Select(x => x + "__CREATE__").ToArray());
            type.RequirePermissions($"{permission}.add");

            var otherPermsUpdate = otherPerms.Where(x => x.Name == permission).SelectMany(x => x.Permissions.Update).ToArray();
            type.RequirePermissions(otherPermsUpdate.Select(x => x + "__UPDATE__").ToArray());
            type.RequirePermissions($"{permission}.update");

            var otherPermsDelete = otherPerms.Where(x => x.Name == permission).SelectMany(x => x.Permissions.Delete).ToArray();
            type.RequirePermissions(otherPermsDelete.Select(x => x + "__DELETE__").ToArray());
            type.RequirePermissions($"{permission}.delete");
        }

        public static void RequirePermissions(this IProvideMetadata type, params string[] permissionsRequired)
        {
            var permissions = type.GetMetadata<HashSet<string>>(PermissionsKey);

            if (permissions == null)
            {
                permissions = new HashSet<string>();
                type.Metadata[PermissionsKey] = permissions;
            }

            foreach (var per in permissionsRequired)
            {
                //Console.WriteLine($"________________permiso agregado {per}");
                permissions.Add(per);
            }

            //if (!string.IsNullOrEmpty(policy))
            // AuthorizationMetadataExtensions.AuthorizeWith(type, "Authorized");
        }

        public static void RequireAuthentication(this IProvideMetadata type)
        {
            AuthorizationMetadataExtensions.AuthorizeWith(type, "Authorized");
        }

        public static void RequirePermission(this IProvideMetadata type, string permission)
        {
            var permissions = type.GetMetadata<List<string>>(PermissionsKey);

            if (permissions == null)
            {
                permissions = new List<string>();
                type.Metadata[PermissionsKey] = permissions;
            }

            permissions.Add(permission);
        }

        public static FieldBuilder<TSourceType, TReturnType> RequirePermission<TSourceType, TReturnType>(
            this FieldBuilder<TSourceType, TReturnType> builder, string permission)
        {
            builder.FieldType.RequirePermission(permission);
            return builder;
        }
    }
}