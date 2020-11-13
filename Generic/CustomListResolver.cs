using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Resolvers;
using GraphQL.Types;
using SER.Graphql.Reflection.NetCore.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SER.Graphql.Reflection.NetCore.Builder;
using Microsoft.AspNetCore.Identity;

namespace SER.Graphql.Reflection.NetCore.Generic
{
    public class CustomListResolver<T> : IFieldResolver where T : class
    {
        private Type _dataType;
        private string _mainTable;
        private Type _parentType;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IDataLoaderContextAccessor _accessor;
        private IDatabaseMetadata _dbMetadata;

        public CustomListResolver(Type dataType,
            Type parentType,
            IHttpContextAccessor httpContextAccessor,
            IDataLoaderContextAccessor accessor,
            IDatabaseMetadata dbMetadata)
        {
            _dataType = dataType;
            _mainTable = parentType.Name;
            _httpContextAccessor = httpContextAccessor;
            _parentType = parentType;
            _accessor = accessor;
            _dbMetadata = dbMetadata;
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

        public IDataLoaderResult<IEnumerable<T>> GetLoader(IResolveFieldContext context, string param)
        {
            Type graphRepositoryType = typeof(IGraphRepository<>).MakeGenericType(new Type[] { _dataType });
            dynamic service = _httpContextAccessor.HttpContext.RequestServices.GetService(graphRepositoryType);
            var first = context.GetArgument<int?>("first");
            //Task<IEnumerable<T>> res = null;
            var metaTable = _dbMetadata.GetTableMetadatas().FirstOrDefault(x => x.Type == context.Source.GetType());
            // Console.WriteLine($"--------------------------- main type {context.Source.GetType()} PK {metaTable.NamePK} _dataType {_dataType.Name}----------------------- ");
            var valueField = context.Source.GetType().GetProperty(metaTable.NamePK).GetValue(context.Source, null);
            IDataLoaderResult<IEnumerable<T>> res = null;
            try
            {
                //IEquatable
                if (context.Source is IBaseModel && valueField is int @int)
                {
                    var loader = _accessor.Context.GetOrAddCollectionBatchLoader<int, T>($"GetItemsByIds_{typeof(T).Name}",
                      (ids) => service.GetItemsByIds(ids, context, param));
                    res = loader.LoadAsync(@int);
                }
                else if (context.Source is IBaseModel && valueField is string @string)
                {
                    var loader = _accessor.Context.GetOrAddCollectionBatchLoader<string, T>($"GetItemsByIds_{typeof(T).Name}",
                      (ids) => service.GetItemsByIds(ids, context, param, isString: true));
                    res = loader.LoadAsync(@string);
                }
                else if (context.Source is IdentityUser)
                {
                    var loader = _accessor.Context.GetOrAddCollectionBatchLoader<string, T>($"GetItemsByIds_{typeof(T).Name}",
                        (ids) => service.GetItemsByIds(ids, context, param, isString: true));
                    res = loader.LoadAsync((context.Source as IdentityUser).Id);
                }
                else if (context.Source is IdentityRole)
                {
                    var loader = _accessor.Context.GetOrAddCollectionBatchLoader<string, T>($"GetItemsByIds_{typeof(T).Name}",
                        (ids) => service.GetItemsByIds(ids, context, param, isString: true));
                    res = loader.LoadAsync((context.Source as IdentityRole).Id);
                }
                else
                {
                    var loader = _accessor.Context.GetOrAddCollectionBatchLoader<string, T>($"GetItemsByIds_{typeof(T).Name}",
                        (ids) => service.GetItemsByIds(ids, context, param, isString: true));
                    res = loader.LoadAsync((string)valueField);
                }

                if (first.HasValue && first.Value > 0)
                {
                    return res;
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
