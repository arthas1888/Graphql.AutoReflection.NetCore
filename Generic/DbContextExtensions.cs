using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.DynamicLinq;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SER.Graphql.Reflection.NetCore.Utilities;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections;

namespace SER.Graphql.Reflection.NetCore.Generic
{
    public static class DbContextExtensions
    {
        public static IQueryable Query(this DbContext context, string entityName) =>
            context.Query(context.Model.FindEntityType(entityName).ClrType);

        static readonly MethodInfo SetMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set));

        public static IQueryable Query(this DbContext context, Type entityType) =>
            (IQueryable)SetMethod.MakeGenericMethod(entityType).Invoke(context, null);

        public static async Task<IEnumerable<T>> GetPagedAsync<T>(this IQueryable<T> source, IHttpContextAccessor _httpContextAccessor,
            FillDataExtensions _fillDataExtensions) where T : class
        {
            bool allowCache = true;
            // Filter By
            if (_httpContextAccessor.HttpContext.Request.Query.Any(x => x.Key.Equals("filter_by")))
            {
                var properties = new Dictionary<string, Type>();

                foreach (var propertyInfo in typeof(T).GetProperties())
                {
                    //Console.WriteLine($"_________________TRACEEEEEEEEEEEEEEEEE____________: key: {propertyInfo.Name} value: {propertyInfo.PropertyType.Name}");
                    if (!propertyInfo.GetCustomAttributes(true).Any(x => x.GetType() == typeof(JsonIgnoreAttribute))
                        && !propertyInfo.GetCustomAttributes(true).Any(x => x.GetType() == typeof(NotMappedAttribute))
                        && !(propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(IList<>))
                        && !(propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                        && !(propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>))
                        && !(typeof(ICollection).IsAssignableFrom(propertyInfo.PropertyType))
                        )
                        properties.Add(propertyInfo.Name, propertyInfo.PropertyType);
                }

                var columnStr = _httpContextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("filter_by")).Value.ToString();
                string pattern = @"\/|\|";
                string[] columns = Regex.Split(columnStr, pattern);
                var divider = new List<string>();
                Match match = Regex.Match(columnStr, pattern);

                // query filtro por AND o OR
                foreach (var (value, index) in columns.Select((v, i) => (v, i)))
                {
                    if (index > 0)
                    {
                        match = match.NextMatch();
                        if (match.Success)
                        {
                            if (match.Value == "/") divider.Add(" AND ");
                            else divider.Add(" OR ");
                        }

                    }
                    else
                    {
                        if (match.Success)
                        {
                            if (match.Value == "/") divider.Add(" AND ");
                            else divider.Add(" OR ");
                        }
                    }
                }

                var expresion = new StringBuilder();
                List<object> values = new List<object>();
                //Procesamiento query
                string dividerOld = "";
                foreach (var (column, index) in columns.Select((v, i) => (v, i)))
                {
                    allowCache = false;
                    if (index > 0)
                    {
                        if (index > 1 && dividerOld != divider[index - 1])
                        {
                            expresion.Append(")");
                            expresion.Append(divider[index - 1]);
                            expresion.Append("(");
                        }
                        else expresion.Append(divider[index - 1]);
                        dividerOld = divider[index - 1];
                    }
                    else expresion.Append("(");

                    var patternStr = @"\=|¬";
                    string[] value = Regex.Split(column, patternStr);
                    if (string.IsNullOrEmpty(value[1])) break;

                    if (value[0] == "$")
                    {
                        try
                        {
                            foreach (var (field, i) in properties.Select((v, i) => (v, i)))
                            {
                                SqlCommandExtension.ConcatFilter(values, expresion, string.Format("@{0}", i + index), field.Key, value[1], column,
                                    typeProperty: field.Value, index: i);
                            }
                            break;
                        }
                        catch (Exception)
                        {
                            throw;
                        }
                    }
                    var paramName = string.Format("@{0}", index);
                    SqlCommandExtension.ConcatFilter(values, expresion, paramName, value[0], value[1], column);

                }
                expresion.Append(")");
                Console.WriteLine(expresion.ToString());
                source = source.Where(expresion.ToString().ToLower(), values.ToArray());
            }

            // Order By
            if (_httpContextAccessor.HttpContext.Request.Query.Any(x => x.Key.Equals("order_by")))
            {
                source = source.OrderBy(_httpContextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("order_by")).Value.ToString());
            }

            // pagination
            if (_httpContextAccessor.HttpContext.Request.Query.Any(x => x.Key.Equals("enable_pagination"))
            && bool.TryParse(_httpContextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("enable_pagination")).Value.ToString(),
               out bool enablePagination))
            {
                var pageSizeRequest = _httpContextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("page_size")).Value;
                var currentPageRequest = _httpContextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("current_page")).Value;
                int pageSize = string.IsNullOrEmpty(pageSizeRequest) ? 20 : int.Parse(pageSizeRequest);
                int pageNumber = string.IsNullOrEmpty(currentPageRequest) ? 1 : int.Parse(currentPageRequest);

                var result = new PagedResultBase();

                result.current_page = pageNumber;
                result.page_size = pageSize;

                int? rowCount = null;
                if (allowCache)
                    rowCount = source.CacheGetOrCreate(_httpContextAccessor);

                result.row_count = rowCount ?? source.CountAsync().Result;

                var pageCount = (double)result.row_count / pageSize;
                result.page_count = (int)Math.Ceiling(pageCount);

                _fillDataExtensions.Add($"{typeof(T).Name.ToLower()}_list", result);

                //foreach (var propertyInfo in typeof(PagedResultBase).GetProperties())
                //{
                //    var currentValue = propertyInfo.GetValue(result);
                //    _fillDataExtensions.Add(propertyInfo.Name, currentValue);
                //}

                return await source.Skip((pageNumber - 1) * pageSize).Take(pageSize).AsNoTracking().ToListAsync();
            }
            return await source.AsNoTracking().ToListAsync();
        }

        private static int CacheGetOrCreate<E>(this IQueryable<E> query, IHttpContextAccessor _contextAccessor)
           where E : class
        {
            var cache = _contextAccessor.HttpContext.RequestServices.GetService<IMemoryCache>();

            var cacheKeySize = string.Format("_{0}_size", typeof(E).Name);
            var cacheEntry = cache.GetOrCreate(cacheKeySize, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromDays(1);
                entry.Size = 1000;
                return query.Count();
            });

            return cacheEntry;
        }

    }
}
