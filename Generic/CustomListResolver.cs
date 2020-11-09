using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Resolvers;
using GraphQL.Types;
using Graphql.AutoReflection.NetCore.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Graphql.AutoReflection.NetCore.Generic
{
    public class CustomListResolver<T> : IFieldResolver where T : class
    {
        private Type _dataType;
        private string _mainTable;
        private Type _parentType;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IDataLoaderContextAccessor _accessor;

        public CustomListResolver(Type dataType, Type parentType, IHttpContextAccessor httpContextAccessor, IDataLoaderContextAccessor accessor)
        {
            _dataType = dataType;
            _mainTable = parentType.Name;
            _httpContextAccessor = httpContextAccessor;
            _parentType = parentType;
            _accessor = accessor;
        }

        public object Resolve(IResolveFieldContext context)
        {
            string paramFK = "";
            //var field = _dataType.GetGenericArguments().Count() > 0 ? _dataType.GetGenericArguments()[0] : null;

            foreach (var (propertyInfo, j) in _dataType.GetProperties().Select((v, j) => (v, j)))
            {
                if (propertyInfo.PropertyType == _parentType)
                {
                    paramFK = propertyInfo.GetCustomAttributes(true)
                        .Where(x => x.GetType() == typeof(ForeignKeyAttribute))
                        .Select(attr => ((ForeignKeyAttribute)attr).Name)
                        .FirstOrDefault();
                    break;
                }
            }


            return GetLoader(context, $"{paramFK}");
        }

        public Task<IEnumerable<T>> GetLoader(IResolveFieldContext context, string param)
        {
            Type graphRepositoryType = typeof(IGraphRepository<>).MakeGenericType(new Type[] { _dataType });
            dynamic service = _httpContextAccessor.HttpContext.RequestServices.GetService(graphRepositoryType);

            var first = context.GetArgument<int?>("first");
            Task<IEnumerable<T>> res = null;
            try
            {
                //if (Assembly.GetCallingAssembly().GetTypes()
                //    .Where(x => !x.IsAbstract && typeof(BasicModel).IsAssignableFrom(x)).Any(x => x == context.Source.GetType()))
                //IEquatable
                if (context.Source is IBaseModel)
                {
                    var loader = _accessor.Context.GetOrAdd($"GetItemsByIds_{typeof(T).Name}", () =>
                       new CollectionBatchDataLoader<int, T>((ids, cancellation) =>
                       {
                           return service.GetItemsByIds(ids, context, param);
                       }, null));

                    res = loader.LoadAsync(((IBaseModel)context.Source).id);

                }
                else // if (context.Source is ApplicationUser || context.Source is ApplicationRole)
                {
                    //var accesor = _httpContextAccessor.HttpContext.RequestServices.GetService<IDataLoaderContextAccessor>();
                    var loader = _accessor.Context.GetOrAddCollectionBatchLoader<string, T>($"GetItemsByIds",
                        (ids) => service.GetItemsByIds(ids, context, param, isString: true));
                    res = loader.LoadAsync("Id");
                }

                if (first.HasValue && first.Value > 0)
                {
                    return res?.ContinueWith(x => x.Result.Take(first.Value));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            return res;
        }

    }
}
