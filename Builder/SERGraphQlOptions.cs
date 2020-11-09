using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace SER.Graphql.Reflection.NetCore.Builder
{
    /// <summary>
    /// Provides various settings needed to configure
    /// the Graphql auto reflection integration.
    /// </summary>
    public class SERGraphQlOptions
    {
        /// <summary>
        /// Gets the services collection.
        /// </summary>
        public IServiceCollection Services { get; set; }

        /// <summary>
        /// Gets or sets the concrete type of the <see cref="DbContext"/> used by the
        /// Graphql auto reflection stores. If this property is not populated,
        /// an exception is thrown at runtime when trying to use the stores.
        /// </summary>
        public Type DbContextType { get; set; }
        public string ConnectionString { get; set; }

        /// <summary>
        /// Configures the GraphQL.Reflection Entity Framework Core stores to use the specified database context type.
        /// </summary>
        /// <typeparam name="TContext">The type of the <see cref="DbContext"/> used by GraphQL.Reflection.</typeparam>
        /// <returns>The <see cref="GraphQLConfiguration"/>.</returns>
        public SERGraphQlOptions UseDbContext<TContext>(IServiceCollection services)
            where TContext : DbContext
            => UseDbContext(typeof(TContext), services);

        /// <summary>
        /// Configures the GraphQL.Reflection Entity Framework Core stores to use the specified database context type.
        /// </summary>
        /// <param name="type">The type of the <see cref="DbContext"/> used by GraphQL.Reflection.</param>
        /// <returns>The <see cref="GraphQLConfiguration"/>.</returns>
        public SERGraphQlOptions UseDbContext(Type type, IServiceCollection services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (!typeof(DbContext).IsAssignableFrom(type))
            {
                throw new ArgumentException("configure dbcontext", nameof(type));
            }

            return Configure(options => options.DbContextType = type);
        }



        /// <summary>
        /// Amends the default GraphQL.Reflection Entity Framework Core configuration.
        /// </summary>
        /// <param name="configuration">The delegate used to configure the GraphQL.Reflection options.</param>
        /// <remarks>This extension can be safely called multiple times.</remarks>
        /// <returns>The <see cref="GraphQLConfiguration"/>.</returns>
        public SERGraphQlOptions Configure(Action<SERGraphQlOptions> configuration)
        {
            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            Services.Configure(configuration);

            return this;
        }
    }
}
