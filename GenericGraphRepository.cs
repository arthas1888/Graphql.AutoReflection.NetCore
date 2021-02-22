using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Linq.Dynamic.Core;
using GraphQL.Types;
using GraphQL.DataLoader;
using GraphQL;
using SER.Graphql.Reflection.NetCore.Models;
using Microsoft.EntityFrameworkCore.DynamicLinq;
using System.ComponentModel.DataAnnotations;
using SER.Graphql.Reflection.NetCore.Utilities;
using SER.Graphql.Reflection.NetCore.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Extensions.Options;
using SER.Graphql.Reflection.NetCore.Builder;
using SER.Utilitties.NetCore.Managers;
using SER.Utilitties.NetCore.Models;

namespace SER.Graphql.Reflection.NetCore
{
    public class GenericGraphRepository<T, TContext, TUser, TRole, TUserRole> : IGraphRepository<T>
            where T : class
            where TContext : DbContext
            where TUser : class
            where TRole : class
            where TUserRole : class
    {
        private readonly TContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly FillDataExtensions _fillDataExtensions;
        private IDataLoaderContextAccessor _dataLoader;
        private IConfiguration _config;
        private IMemoryCache _cache;
        private readonly ILogger _logger;
        public string model;
        public string nameModel;
        private readonly AuditManager _cRepositoryLog;
        public static readonly ILoggerFactory MyLoggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddFilter((category, level) => category == DbLoggerCategory.Database.Command.Name
                        && level == LogLevel.Information)
                .AddConsole();
        });
        private readonly IOptionsMonitor<SERGraphQlOptions> _optionsDelegate;

        public GenericGraphRepository(TContext db,
            IHttpContextAccessor httpContextAccessor,
            FillDataExtensions fillDataExtensions,
            IDataLoaderContextAccessor dataLoader,
            IConfiguration config,
            IOptionsMonitor<SERGraphQlOptions> optionsDelegate)
        {
            _context = db;
            _config = config;
            _httpContextAccessor = httpContextAccessor;
            model = typeof(T).Name;
            nameModel = typeof(T).Name.ToSnakeCase().ToLower();
            _cache = _httpContextAccessor.HttpContext.RequestServices.GetService<IMemoryCache>();
            _logger = _httpContextAccessor.HttpContext.RequestServices.GetService<ILogger<GenericGraphRepository<T, TContext, TUser, TRole, TUserRole>>>();
            _fillDataExtensions = fillDataExtensions;
            _dataLoader = dataLoader;
            _optionsDelegate = optionsDelegate;
            _cRepositoryLog = httpContextAccessor.HttpContext.RequestServices.GetService<AuditManager>();
        }

        public string GetCompanyIdUser()
        {
            return _httpContextAccessor.HttpContext.User.Claims.FirstOrDefault(x =>
                x.Type == GraphClaimTypes.CompanyId)?.Value;
        }

        public string GetCurrentUser()
        {
            return _httpContextAccessor.HttpContext.User.Claims.FirstOrDefault(x =>
                x.Type == Claims.Subject)?.Value;
        }

        public string GetCurrenUserName()
        {
            return _httpContextAccessor.HttpContext.User.Claims.FirstOrDefault(x => x.Type == Claims.Name)?.Value;
        }

        public List<string> GetRolesUser()
        {
            return _httpContextAccessor.HttpContext.User.Claims.Where(x =>
                x.Type == Claims.Role).Select(x => x.Value).ToList();
        }

        public async Task<IEnumerable<T>> GetAllAsync(string alias, List<string> includeExpressions = null,
            string orderBy = "", string whereArgs = "", int? take = null, int? offset = null, params object[] args)
        {
            return await GetQuery(alias, includeExpressions: includeExpressions, orderBy: orderBy,
                first: take, offset: offset, whereArgs: whereArgs, args: args)
                .AsNoTracking().ToListAsync();
        }

        public IQueryable<T> GetQuery(string alias, List<string> includeExpressions = null,
            string orderBy = "", string whereArgs = "", int? first = null, int? offset = null, params object[] args)
        {
            IQueryable<T> query = GetModel;

            if (includeExpressions != null && includeExpressions.Count > 0)
            {
                foreach (var include in includeExpressions)
                    query = query.Include(include);
            }
            if (!string.IsNullOrEmpty(whereArgs) && args.Length > 0)
                query = query.Where(whereArgs, args);

            if (_optionsDelegate.CurrentValue.EnableCustomFilter)
                query = FilterQueryByCompany(query, out _);

            if (!string.IsNullOrEmpty(orderBy))
                query = query.OrderBy(orderBy);

            if (offset != null && first != null)
            {
                var result = new PagedResultBase();

                result.current_page = offset.Value;
                result.page_size = first.Value;
                var allowCache = args.Length == 0;

                int? rowCount = null;
                if (allowCache)
                    rowCount = CacheGetOrCreate(query);

                result.row_count = rowCount ?? query.CountAsync().Result;

                var pageCount = (double)result.row_count / first.Value;
                result.page_count = (int)Math.Ceiling(pageCount);

                _fillDataExtensions.Add(alias, result);
                query = query.Skip((offset.Value - 1) * first.Value);
            }
            if (first != null)
            {
                query = query.Take(first.Value);
            }

            return query;
        }

        public int GetCountQuery(List<string> includeExpressions = null,
           string whereArgs = "", params object[] args)
        {
            IQueryable<T> query = GetModel;

            if (includeExpressions != null && includeExpressions.Count > 0)
            {
                foreach (var include in includeExpressions)
                    query = query.Include(include);
            }
            if (!string.IsNullOrEmpty(whereArgs) && args.Length > 0)
                query = query.Where(whereArgs, args);

            if (_optionsDelegate.CurrentValue.EnableCustomFilter)
                query = FilterQueryByCompany(query, out _);
            return query.Count();
        }

        private IQueryable<T> FilterQueryByCompany(IQueryable<T> query, out bool find, Type parentType = null, string columnName = "")
        {
            string nameField = _optionsDelegate.CurrentValue.NameCustomFilter;
            find = false;
            string companyId = null;
            var types = new Dictionary<string, Type>();
            var typeToEvaluate = typeof(T);
            if (parentType != null) typeToEvaluate = parentType;

            //Console.WriteLine($"Name {_httpContextAccessor.HttpContext.User.Identity.Name} IsAuthenticated {_httpContextAccessor.HttpContext.User.Identity.IsAuthenticated}" +
            //    $" GetCompanyIdUser() { GetCompanyIdUser()}");
            foreach (var propertyInfo in typeToEvaluate.GetProperties())
            {
                var field = propertyInfo.PropertyType;
                if (field.IsGenericType && field.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    field = field.GetGenericArguments()[0];
                }
                var childType = field ?? propertyInfo.GetType();
                var attrName = "";
                foreach (ForeignKeyAttribute attr in propertyInfo.GetCustomAttributes(true).Where(x => x.GetType() == typeof(ForeignKeyAttribute)))
                {
                    attrName = attr.Name;
                }

                if (attrName != "" && typeToEvaluate.GetProperties().SingleOrDefault(x => x.Name == attrName).GetCustomAttributes(true).Any(x => x.GetType() == typeof(RequiredAttribute)))
                    if (typeof(IBaseModel).IsAssignableFrom(childType)
                        || childType == typeof(TUser) || childType == typeof(TRole) || childType == typeof(TUserRole))
                    {
                        types.Add(propertyInfo.Name, childType);
                    }


                if (propertyInfo.Name == nameField)
                {
                    find = true;
                    if (_httpContextAccessor.HttpContext.User.Identity.IsAuthenticated && !string.IsNullOrEmpty(_httpContextAccessor.HttpContext.User.Identity.Name))
                        companyId = GetCompanyIdUser();
                    else
                        companyId = _httpContextAccessor.HttpContext.Session?.GetInt32(nameField)?.ToString();

                    // if (propertyInfo.Name == "CompanyId") query = query.Where($"{columnName}CompanyId  = @0 OR {columnName}CompanyId  == null", companyId);
                    query = query.Where($"{columnName}{nameField}  = @0 OR {columnName}{nameField}  == null", companyId);
                    break;
                }
            }

            if (!find)
            {
                foreach (var dict in types.OrderByDescending(x => x.Key))
                {
                    //_logger.LogWarning($"---------------dict: {dict.Key}");
                    query = FilterQueryByCompany(query, out bool finded, dict.Value, $"{dict.Key}.");
                    if (finded) break;
                }
            }

            return query;
        }

        public async Task<ILookup<Tkey, T>> GetItemsByIds<Tkey>(IEnumerable<Tkey> ids, IResolveFieldContext context, string param,
            bool isString = false)
        // where Tkey : struct
        {
            var whereArgs = new StringBuilder();
            var args = new List<object>();
            var orderBy = context.GetArgument<string>("orderBy");

            string SqlConnectionStr = _optionsDelegate.CurrentValue.ConnectionString;
            var optionsBuilder = new DbContextOptionsBuilder<TContext>();
            optionsBuilder.UseNpgsql(SqlConnectionStr, o => o.UseNetTopologySuite());
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.UseLoggerFactory(MyLoggerFactory);

            using DbContext _dbContext = (DbContext)Activator.CreateInstance(typeof(TContext), new object[] { optionsBuilder.Options });
            // using var _db = new DbContext(optionsBuilder.Options);
            IQueryable<T> query = _dbContext.Set<T>();

            List<string> includeExpressions = new List<string>();
            GraphUtils.DetectChild<TUser, TRole, TUserRole>(context.FieldAst.SelectionSet.Selections, includeExpressions,
                   ((dynamic)context.FieldDefinition.ResolvedType).ResolvedType, args, whereArgs,
                   arguments: context.Arguments, mainType: typeof(T));

            if (whereArgs.Length > 0)
                whereArgs.Append(" and ");

            if (isString) whereArgs.Append($"@{args.Count}.Contains({param})");
            else
                whereArgs.Append($"@{args.Count}.Contains(int({param}))");
            args.Add(ids);

            if (includeExpressions != null && includeExpressions.Count > 0)
            {
                foreach (var include in includeExpressions)
                    query = query.Include(include);
            }

            _logger.LogWarning($"whereArgs: {whereArgs}");
            query = query.Where(whereArgs.ToString(), args.ToArray());

            if (_optionsDelegate.CurrentValue.EnableCustomFilter)
                query = FilterQueryByCompany(query, out _);

            if (!string.IsNullOrEmpty(orderBy))
                query = query.OrderBy(orderBy);

            var items = await query.AsNoTracking().ToListAsync();
            var pi = typeof(T).GetProperty(param);
            //if (typeof(Tkey) == typeof(int))
            return items.ToLookup(x => (Tkey)pi.GetValue(x, null));
        }


        public async Task<T> GetByIdAsync(string alias, int id, List<string> includeExpressions = null,
          string whereArgs = "", params object[] args)
        {
            if (id == 0) return null;
            var entity = await GetQuery(alias, includeExpressions: includeExpressions,
                first: 1, whereArgs: whereArgs, args: args)
                .AsNoTracking().FirstOrDefaultAsync();

            //var entity = await GetModel.FindAsync(id);
            if (entity == null) return null;
            return entity;
        }

        public async Task<T> GetByIdAsync(string alias, string id, List<string> includeExpressions = null,
         string whereArgs = "", params object[] args)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var entity = await GetQuery(alias, includeExpressions: includeExpressions,
                first: 1, whereArgs: whereArgs, args: args)
                .AsNoTracking().FirstOrDefaultAsync();
            if (entity == null) return null;
            return entity;
        }

        private DbSet<T> GetModel
        {
            get { return _context.Set<T>(); }
        }

        public async Task<T> Create(T entity, string alias = "")
        {
            // var objstr = JsonSerializer.Serialize(entity);
            //_logger.LogInformation($"----------------------------objstr {objstr}");

            var cacheKeySize = string.Format("_{0}_size", model);
            _cache.Remove(cacheKeySize);
            nameModel = $"create_{nameModel}";
            try
            {
                entity.GetType().GetProperty("created_by_id")?.SetValue(entity, GetCurrentUser(), null);

                var obj = _context.Add(entity);
                _context.SaveChanges();

                if (_optionsDelegate.CurrentValue.EnableAudit)
                {
                    await _cRepositoryLog.AddLog(_context, new AuditBinding()
                    {
                        action = AudiState.CREATE,
                        objeto = typeof(T).Name,
                    }, id: GetKey(entity), commit: true);
                }

                SendStatus(GraphGrpcStatus.CREATE, GetKey(entity));

                return obj.Entity;
            }
            catch (ValidationException exc)
            {
                _logger.LogError(exc, $"{nameof(Create)} validation exception: {exc?.Message}");
                _fillDataExtensions.Add($"{(string.IsNullOrEmpty(alias) ? nameModel : alias)}", $"{exc?.Message }");
                _context.Entry(entity).State = EntityState.Detached;

            }
            catch (DbUpdateException e)
            {
                _logger.LogError(e, $"{nameof(Create)} db update error: {e?.InnerException?.Message}");
                _fillDataExtensions.Add($"{(string.IsNullOrEmpty(alias) ? nameModel : alias)}", $"{ e.InnerException?.Message }");
                _context.Entry(entity).State = EntityState.Detached;
            }
            return entity;
        }

        public virtual string GetKey(T entity)
        {
            var keyName = _context.Model.FindEntityType(typeof(T)).FindPrimaryKey()?.Properties
                .Select(x => x.Name).FirstOrDefault(); // .Single();
            return entity.GetType().GetProperty(keyName).GetValue(entity, null).ToString();
        }

        public Task<T> Update(int id, T entity, string alias = "")
        {
            throw new NotImplementedException();
        }

        public async Task<T> Update(int id, T entity, Dictionary<string, object> dict, string alias = "")
        {
            var obj = GetModel.Find(id);
            if (obj != null)
            {
                nameModel = $"update_{nameModel}";
                try
                {
                    foreach (var values in dict)
                    {
                        try
                        {
                            var propertyInfo = typeof(T).GetProperty(values.Key);
                            if (propertyInfo.Name == "id") continue;

                            var oldValue = propertyInfo.GetValue(obj);
                            var newValue = propertyInfo.GetValue(entity);

                            // if (newValue == null && oldValue != null) continue;
                            // if (newValue == oldValue) continue;

                            Type type = null;
                            var isList = propertyInfo.PropertyType.Name.Contains("List");
                            if (isList)
                                type = propertyInfo.PropertyType.GetGenericArguments().Count() > 0 ?
                                    propertyInfo.PropertyType.GetGenericArguments()[0] : propertyInfo.PropertyType;

                            if (isList && type.BaseType == typeof(object) && newValue != null)
                                DeleteRelationsM2M(type, id);

                            //Console.WriteLine($"___________TRACEEEEEEEEEEEEEEEEE____________: key: {propertyInfo.Name} {oldValue} {newValue}");
                            propertyInfo.SetValue(obj, newValue, null);

                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }

                    if (_optionsDelegate.CurrentValue.EnableAudit)
                    {
                        var modified = await _cRepositoryLog.AddLog(_context, new AuditBinding()
                        {
                            action = AudiState.UPDATE,
                            objeto = typeof(T).Name,
                        }, id: id.ToString());
                        var propertyInfo = typeof(T).GetProperties().FirstOrDefault(x => x.Name == "last_movement");
                        if (propertyInfo != null)
                            obj.GetType().GetProperty(propertyInfo.Name)?.SetValue(obj, modified, null);

                        obj.GetType().GetProperty("update_date")?.SetValue(obj, DateTime.UtcNow, null);
                        obj.GetType().GetProperty("updated_by_id")?.SetValue(obj, GetCurrentUser(), null);
                    }

                    //_context.Entry(obj).State = EntityState.Modified;
                    _context.SaveChanges();
                    SendStatus(GraphGrpcStatus.UPDATE, id.ToString());
                }
                catch (ValidationException exc)
                {
                    _logger.LogError(exc, $"{nameof(Update)} validation exception: {exc?.Message}");
                    _fillDataExtensions.Add($"{(string.IsNullOrEmpty(alias) ? nameModel : alias)}", @$"{id} => { exc?.Message}");
                    _context.Entry(obj).State = EntityState.Detached;

                }
                catch (DbUpdateException e)
                {
                    _context.Entry(obj).State = EntityState.Detached;
                    _fillDataExtensions.Add($"{(string.IsNullOrEmpty(alias) ? nameModel : alias)}", $"{id} => {e.InnerException?.Message}");
                }
            }
            return obj;
        }

        private void DeleteRelationsM2M(Type type, int parentId)
        {
            try
            {
                GetType()
                    .GetMethod("DeleteRelations")
                    .MakeGenericMethod(type)
                    .Invoke(this, parameters: new object[] { parentId });
            }
            catch (Exception e)
            {
                _logger.LogError($"ERROR {e.ToString()} type {nameof(type)} parentId {parentId}");
            }
        }

        public void DeleteRelations<M>(int parentId) where M : class
        {
            var iQueryable = _context.Set<M>();
            var keyProperty = typeof(M).GetProperties();
            var paramFK = "";
            Type valueType = null;

            foreach (var prop in typeof(M).GetProperties())
            {
                var field = prop.PropertyType;
                if (field.IsGenericType && field.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    field = field.GetGenericArguments()[0];
                }

                if (field == typeof(T))
                {
                    paramFK = prop.GetCustomAttributes(true)
                        .Where(x => x.GetType() == typeof(ForeignKeyAttribute))
                        .Select(attr => ((ForeignKeyAttribute)attr).Name)
                        .FirstOrDefault();
                    valueType = field;
                    break;
                }
            }
            //Console.WriteLine($"----------------------------------llega aca!!!!!!!!!!!!!!!! parentId {parentId} paramFK {paramFK} paramType {valueType}");

            //var paramToEvaluate = typeof(M).GetProperties().FirstOrDefault(x => x.PropertyType.Name == paramFK).PropertyType;
            //if (paramToEvaluate.IsGenericType && paramToEvaluate.GetGenericTypeDefinition() == typeof(Nullable<>))
            //{
            //    paramToEvaluate = paramToEvaluate.GetGenericArguments()[0];
            //    paramFK = paramToEvaluate.Name;
            //}
            if (!string.IsNullOrEmpty(paramFK))
            {
                valueType = typeof(M).GetProperty(paramFK).PropertyType;
                var expToEvaluate = EqualPredicate<M>(typeof(M), paramFK, parentId, valueType);
                iQueryable.RemoveRange(iQueryable.Where(expToEvaluate));
                // _context.SaveChanges();
            }
        }

        private Expression<Func<M, bool>> EqualPredicate<M>(Type type, string propertyName, object value, Type valueType) where M : class
        {
            var parameter = Expression.Parameter(type, "x");
            // x => x.id == value
            //     |___|
            var property = Expression.Property(parameter, propertyName);

            // x => x.id == value
            //             |__|
            var numberValue = Expression.Convert(Expression.Constant(value), valueType); // Expression.Constant(value);

            // x => x.id == value
            //|________________|
            var exp = Expression.Equal(property, numberValue);
            return Expression.Lambda<Func<M, bool>>(exp, parameter);
        }

        public async Task<T> Delete(int id, string alias = "")
        {
            var obj = GetModel.Find(id);
            if (obj != null)
            {
                nameModel = $"delete_{nameModel}";
                try
                {
                    //GetModel.Remove(obj);
                    _context.Entry(obj).State = EntityState.Deleted;
                    _context.SaveChanges();

                    if (_optionsDelegate.CurrentValue.EnableAudit)
                    {
                        await _cRepositoryLog.AddLog(_context, new AuditBinding()
                        {
                            action = AudiState.DELETE,
                            objeto = typeof(T).Name,
                        }, id: id.ToString(), commit: true);
                    }
                }
                catch (ValidationException exc)
                {
                    _logger.LogError(exc, $"{nameof(Update)} validation exception: {exc?.Message}");
                    _context.Entry(obj).State = EntityState.Detached;
                    _fillDataExtensions.Add($"{(string.IsNullOrEmpty(alias) ? nameModel : alias)}", @$"{id} => {exc?.Message}");
                }
                catch (DbUpdateException e)
                {
                    _context.Entry(obj).State = EntityState.Detached;
                    _fillDataExtensions.Add($"{(string.IsNullOrEmpty(alias) ? nameModel : alias)}", $"{id} => {e.InnerException?.Message}");
                }

                var cacheKeySize = string.Format("_{0}_size", model);
                _cache.Remove(cacheKeySize);
                SendStatus(GraphGrpcStatus.DELETE, id.ToString());
            }
            return obj;
        }

        #region helpers
        enum GraphGrpcStatus
        {
            CREATE, UPDATE, DELETE
        }

        private void SendStatus(GraphGrpcStatus action, string id)
        {
            if (_optionsDelegate.CurrentValue.EnableStatusMutation)
            {
                try
                {
                    _optionsDelegate.CurrentValue.CallbackStatus.Invoke(new GraphStatusRequest
                    {
                        ClassName = typeof(T).Name,
                        Action = (int)action,
                        Id = id,
                        CompanyId = GetCompanyIdUser()
                    });
                }
                catch (Exception e)
                {
                    _logger.LogError($"error Callback : {e}");
                    throw;
                }
            }
        }
        #endregion

        #region utilities
        public int CacheGetOrCreate<E>(IQueryable<E> query)
             where E : class
        {
            var cacheKeySize = string.Format("_{0}_size", typeof(T).Name);
            var cacheEntry = _cache.GetOrCreate(cacheKeySize, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromDays(1);
                entry.Size = 1000;
                return query.Count();
            });

            return cacheEntry;
        }

        #endregion
    }

}
