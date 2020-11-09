using GraphQL;
using GraphQL.Authorization;
using GraphQL.DataLoader;
using GraphQL.Server;
using GraphQL.Types;
using GraphQL.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SER.Graphql.Reflection.NetCore.Generic;
using SER.Graphql.Reflection.NetCore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SER.Graphql.Reflection.NetCore.Builder;
using Microsoft.EntityFrameworkCore;

namespace SER.Graphql.Reflection.NetCore.Custom
{
    public static class ServiceCollectionExtensions
    {
        public static void AddConfigGraphQl<TContext, TUser, TRole, TUserRole>(this IServiceCollection services)
            where TContext : DbContext
            where TUser : class
            where TRole : class
            where TUserRole : class
        {
            services.AddSingleton<IDocumentExecuter, DocumentExecuter>();
            services.AddSingleton<ITableNameLookup, TableNameLookup>();
            services.AddSingleton<TableMetadata>();
            services.AddSingleton<IDatabaseMetadata, DatabaseMetadata<TContext>>();

            services.AddSingleton<IDataLoaderContextAccessor, DataLoaderContextAccessor>();
            services.AddSingleton<DataLoaderDocumentListener>();

            services.AddScoped<GraphQLQuery>();
            services.AddScoped<FillDataExtensions>();
            services.AddScoped<ISchema, AppSchema>();

            //permissions
            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.TryAddSingleton<IAuthorizationEvaluator, AuthorizationEvaluator>();

            // services.AddTransient<IValidationRule, AuthorizationValidationRule>();

            services.TryAddSingleton(s =>
            {
                var authSettings = new AuthorizationSettings();
                // authSettings.AddPolicy("AdminPolicy", _ => _.RequireClaim("role", "Admin"));
                authSettings.AddPolicy("Authorized", _ => _.RequireAuthenticatedUser());
                return authSettings;
            });

            services.AddLogging(builder => builder.AddConsole());
            services.AddHttpContextAccessor();
            services
                .AddGraphQL(o =>
                {
                    o.ExposeExceptions = false; // CurrentEnvironment.IsDevelopment();
                    o.EnableMetrics = false; // CurrentEnvironment.IsDevelopment();
                    o.UnhandledExceptionDelegate = ctx => Console.WriteLine("error: " + ctx.OriginalException.Message);
                })
                .AddSystemTextJson()
                .AddUserContextBuilder(ctx => new GraphQLUserContext { User = ctx.User })
                .AddDataLoader()
                .AddGraphTypes(ServiceLifetime.Scoped);

            var config = new GraphQLConfiguration(services);
            config.UseDbContext<TContext>();

            services.AddScoped<IGraphRepository<TUser>, GenericGraphRepository<TUser, TContext>>();
            services.AddScoped<IGraphRepository<TRole>, GenericGraphRepository<TRole, TContext>>();
            services.AddScoped<IGraphRepository<TUserRole>, GenericGraphRepository<TUserRole, TContext>>();
            services.AddScoped<IGraphRepository<IdentityRoleClaim<string>>, GenericGraphRepository<IdentityRoleClaim<string>, TContext>>();
            AddScopedModelsDynamic<TContext>(services);
        }

        public static void AddScopedModelsDynamic<TContext>(this IServiceCollection services)
             where TContext : DbContext
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == typeof(TContext).Assembly.GetName().Name);
            // var assembly = Assembly.GetCallingAssembly();

            foreach (var type in assembly.GetTypes()
                .Where(x => !x.IsAbstract && typeof(IBaseModel).IsAssignableFrom(x)))
            {
                var interfaceType = typeof(IGraphRepository<>).MakeGenericType(new Type[] { type });
                var inherateType = typeof(GenericGraphRepository<,>).MakeGenericType(new Type[] { type, typeof(TContext) });
                ServiceLifetime serviceLifetime = ServiceLifetime.Scoped;
                // Console.WriteLine($"Dependencia IGraphRepository registrada type {type.Name}");
                services.TryAdd(new ServiceDescriptor(interfaceType, inherateType, serviceLifetime));
            }
        }
    }
}
