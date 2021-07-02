using GraphQL;
using GraphQL.Authorization;
using GraphQL.DataLoader;
using GraphQL.Resolvers;
using GraphQL.Server.Transports.Subscriptions.Abstractions;
using GraphQL.Subscription;
using GraphQL.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SER.Graphql.Reflection.NetCore.Builder;
using SER.Graphql.Reflection.NetCore.Generic;
using SER.Graphql.Reflection.NetCore.Utilities;
using SER.Graphql.Reflection.NetCore.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore
{
    public class AppSubscriptions : ObjectGraphType<object>
    {

        private readonly IDatabaseMetadata _dbMetadata;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ITableNameLookup _tableNameLookup;
        private readonly IOptionsMonitor<SERGraphQlOptions> _optionsDelegate;
        private readonly IDataLoaderContextAccessor _accessor;

        public AppSubscriptions(
            IDatabaseMetadata dbMetadata,
            ITableNameLookup tableNameLookup,
            IHttpContextAccessor httpContextAccessor,
            IOptionsMonitor<SERGraphQlOptions> optionsDelegate,
            IDataLoaderContextAccessor accessor
            )
        {
            _dbMetadata = dbMetadata;
            _httpContextAccessor = httpContextAccessor;
            _tableNameLookup = tableNameLookup;
            _optionsDelegate = optionsDelegate;
            _accessor = accessor;

            this.RequireAuthentication();
            Name = "subscription";

            foreach (var metaTable in _dbMetadata.GetTableMetadatas())
            {
                if (metaTable.Type == _optionsDelegate.CurrentValue.UserType
                     || metaTable.Type == _optionsDelegate.CurrentValue.RoleType
                     || metaTable.Type == _optionsDelegate.CurrentValue.UserRoleType) continue;

                var type = metaTable.Type;
                var friendlyTableName = type.Name.ToLower().ToSnakeCase();

                dynamic objectGraphType = null;
                if (!_tableNameLookup.ExistGraphType(metaTable.Type.Name))
                {
                    var inherateType = typeof(TableType<>).MakeGenericType(new Type[] { metaTable.Type });
                    objectGraphType = Activator.CreateInstance(inherateType, new object[] { metaTable,
                        _dbMetadata, _tableNameLookup, _httpContextAccessor, _accessor, _optionsDelegate });
                }

                var tableType = _tableNameLookup.GetOrInsertGraphType(metaTable.Type.Name, objectGraphType);

                var inherateResolverType = typeof(AppFuncFieldResolver<>).MakeGenericType(new Type[] { metaTable.Type });
                dynamic funcFieldResolver = Activator.CreateInstance(inherateResolverType);

                var inherateEventStreamType = typeof(AppEventStreamResolver<>).MakeGenericType(new Type[] { metaTable.Type });
                dynamic eventStreamResolver = Activator.CreateInstance(inherateEventStreamType, new object[] { _httpContextAccessor });

                AddField(new EventStreamFieldType
                {
                    Name = $"subsciption_{friendlyTableName}",
                    Type = tableType.GetType(),
                    ResolvedType = tableType,
                    Resolver = funcFieldResolver,
                    Subscriber = eventStreamResolver,
                    Arguments = new QueryArguments(
                        new QueryArgument<StringGraphType> { Name = "field" },
                        new QueryArgument<IntGraphType> { Name = "value" }
                    ),
                });
            }
        }

    }

    public class AppFuncFieldResolver<TReturnType> : IFieldResolver<TReturnType>, IFieldResolver
    {

        public TReturnType Resolve(IResolveFieldContext context)
        {
            return (TReturnType)context.Source;
        }

        object IFieldResolver.Resolve(IResolveFieldContext context) => Resolve(context);
    }

    public class AppEventStreamResolver<T> : IEventStreamResolver<T>, IEventStreamResolver
        where T : class
    {

        private readonly IHttpContextAccessor _httpContextAccessor;

        public AppEventStreamResolver(
            IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public IObservable<T> Subscribe(IResolveEventStreamContext context)
        {

            Console.WriteLine($" User --------------------------- IsAuthenticated {_httpContextAccessor.HttpContext.User?.Identity?.IsAuthenticated}");

            if (_httpContextAccessor.HttpContext.User?.Identity == null || !_httpContextAccessor.HttpContext.User.Identity.IsAuthenticated)
            {
                context.Errors.Add(new ExecutionError($"Missing Bearer Token"));
                return new List<T>().ToObservable();
            }

            string field = context.GetArgument<string>("field");
            int value = context.GetArgument<int>("value");

            IHandleMsg<T> service = (IHandleMsg<T>)_httpContextAccessor.HttpContext.RequestServices.GetService(typeof(IHandleMsg<T>));
            var objs = service.ObservableObj();
            if (!string.IsNullOrEmpty(field) && value > 0)
            {
                var pi = typeof(T).GetProperty(field);
                if (pi != null)
                {
                    return objs.Where(x => (int)pi.GetValue(x) == (int)value);
                }
                else
                {
                    context.Errors.Add(new ExecutionError($"Bad params"));
                    return new List<T>().ToObservable();
                }
            }

            return objs;
        }


        IObservable<object> IEventStreamResolver.Subscribe(IResolveEventStreamContext context) => Subscribe(context);
    }
}

