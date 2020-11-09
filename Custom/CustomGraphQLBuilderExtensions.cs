using GraphQL.Server;
using GraphQL.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SER.Graphql.Reflection.NetCore.Custom
{
    public static class CustomGraphQLBuilderExtensions
    {
        /// <summary>
        /// Add all types that implement <seealso cref="IGraphType"/> in the calling assembly
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="serviceLifetime">The service lifetime to register the GraphQL types with</param>
        /// <returns>Reference to <paramref name="builder"/>.</returns>
        public static IGraphQLBuilder CustomAddGraphTypes(
            this IGraphQLBuilder builder,
            IConfiguration config,
            ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
            => builder.CustomAddGraphTypes(Assembly.GetCallingAssembly(), config, serviceLifetime);

        /// <summary>
        /// Add all types that implement <seealso cref="IGraphType"/> in the assembly which <paramref name="typeFromAssembly"/> belongs to
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="typeFromAssembly">The type from assembly to register all GraphQL types from</param>
        /// <param name="serviceLifetime">The service lifetime to register the GraphQL types with</param>
        /// <returns>Reference to <paramref name="builder"/>.</returns>
        public static IGraphQLBuilder CustomAddGraphTypes(
            this IGraphQLBuilder builder,
            Type typeFromAssembly,
            IConfiguration config,
            ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
            => builder.CustomAddGraphTypes(typeFromAssembly.Assembly, config, serviceLifetime);

        /// <summary>
        /// Add all types that implement <seealso cref="IGraphType"/> in the specified assembly
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="assembly">The assembly to register all GraphQL types from</param>
        /// <param name="serviceLifetime">The service lifetime to register the GraphQL types with</param>
        /// <returns>Reference to <paramref name="builder"/>.</returns>
        public static IGraphQLBuilder CustomAddGraphTypes(
            this IGraphQLBuilder builder,
            Assembly assembly,
            IConfiguration config,
            ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        {
            // Register all GraphQL types
            foreach (var type in assembly.GetTypes()
                .Where(x => !x.IsAbstract && typeof(IGraphType).IsAssignableFrom(x)))
            {
                Console.WriteLine($"dependencia registrada type {type.Name}");
                builder.Services.TryAdd(new ServiceDescriptor(type, type, serviceLifetime));
            }

            return builder;
        }
    }
}
