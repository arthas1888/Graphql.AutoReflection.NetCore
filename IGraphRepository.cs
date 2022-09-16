using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore
{
    public interface IGraphRepository<T> where T : class
    {
        Task<T> GetFirstAsync(string alias, List<string> includeExpressions = null, string whereArgs = "", Dictionary<string, object> customfilters = null, params object[] args);

        Task<IEnumerable<T>> GetAllAsync(string alias, List<string> includeExpressions = null,
            string orderBy = "", string whereArgs = "", int? take = null, int? offset = null, Dictionary<string, object> customfilters = null, params object[] args);
        IQueryable<T> GetQuery(string alias, List<string> includeExpressions = null,
            string orderBy = "", string whereArgs = "", int? take = null, int? offset = null, Dictionary<string, object> customfilters = null, params object[] args);
        Task<T> GetByIdAsync(string alias, int id, List<string> includeExpressions = null,
          string whereArgs = "", Dictionary<string, object> customfilters = null, params object[] args);
        Task<T> Create(T entity, string alias = "", bool sendObjFirebase = true, List<string> includeExpressions = null);
        Task<T> Update(object id, T entity, string alias = "", bool sendObjFirebase = true, List<string> includeExpressions = null);
        Task<T> Update(object id, T entity, Dictionary<string, object> dict, string alias = "", bool sendObjFirebase = true, List<string> includeExpressions = null);
        Task<T> Update(object id, Dictionary<string, object> entity, Dictionary<string, object> dict, string alias = "", bool sendObjFirebase = true, List<string> includeExpressions = null);
        Task<T> Delete(object id, string alias = "", bool sendObjFirebase = true);
    }
}
