using GraphQL.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Newtonsoft.Json.Linq;
using SER.Graphql.Reflection.NetCore.Utilities;
using SER.Graphql.Reflection.NetCore.Models;

namespace SER.Graphql.Reflection.NetCore.Generic
{
    public interface IDatabaseMetadata
    {
        void ReloadMetadata();
        IEnumerable<TableMetadata> GetTableMetadatas();
    }

    public sealed class DatabaseMetadata<TContext> : IDatabaseMetadata where TContext: DbContext
    {
        private readonly ITableNameLookup _tableNameLookup;
        private readonly IConfiguration _config;
        private IEnumerable<TableMetadata> _tables;

        public DatabaseMetadata(
            ITableNameLookup tableNameLookup,
            IConfiguration config)
        {
            _config = config;
            _tableNameLookup = tableNameLookup;
            //_databaseName = _dbContext.Database.GetDbConnection().Database; 
            if (_tables == null)
                ReloadMetadata();
        }
        public IEnumerable<TableMetadata> GetTableMetadatas()
        {
            if (_tables == null)
                return new List<TableMetadata>();
            return _tables;
        }
        public void ReloadMetadata()
        {
            _tables = FetchTableMetaData();
        }
        private IReadOnlyList<TableMetadata> FetchTableMetaData()
        {
            var metaTables = new List<TableMetadata>();

            string SqlConnectionStr = _config.GetConnectionString("PsqlConnection");
            var optionsBuilder = new DbContextOptionsBuilder<TContext>();
            optionsBuilder.UseNpgsql(SqlConnectionStr, o => o.UseNetTopologySuite());
            using DbContext _dbContext = (DbContext)Activator.CreateInstance(typeof(TContext), new object[] { optionsBuilder.Options });
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == _dbContext.GetType().Assembly.GetName().Name);

            foreach (var entityType in _dbContext.Model.GetEntityTypes())
            {
                var tableName = entityType.GetTableName();
                if (Constantes.SystemTablesSnakeCase.Contains(tableName))
                {
                    continue;
                }
                var elementType = assembly.GetTypes().Where(x => !x.IsAbstract && typeof(IBaseModel).IsAssignableFrom(x))
                    .FirstOrDefault(x => x == entityType.ClrType);
                // Type elementType = Type.GetType(entityType.Name);
                // Console.WriteLine($"tabla evaluada Name {entityType.Name} type3 {type3} elementType {elementType} {entityType.ClrType} ");

                metaTables.Add(new TableMetadata
                {
                    TableName = tableName,
                    AssemblyFullName = entityType.ClrType.FullName,
                    Columns = GetColumnsMetadata(entityType, elementType),
                    Type = elementType ?? entityType.ClrType
                });
                _tableNameLookup.InsertKeyName(tableName);

            }

            return metaTables;
        }

        private IReadOnlyList<ColumnMetadata> GetColumnsMetadata(IEntityType entityType, Type type)
        {
            var tableColumns = new List<ColumnMetadata>();

            if (type != null)
            {
                foreach (var propertyType in type.GetProperties())
                {
                    var field = propertyType.PropertyType;
                    if (field.IsGenericType && field.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        field = field.GetGenericArguments()[0];
                    }

                    var isList = propertyType.PropertyType.Name.Contains("List");
                    if (isList)
                        field = propertyType.PropertyType.GetGenericArguments().Count() > 0 ? propertyType.PropertyType.GetGenericArguments()[0] : propertyType.PropertyType;

                    //Console.WriteLine($"Columna de la tabla: {entityType.GetTableName()} Name: {propertyType.Name} " +
                    //    $"Type: {propertyType.GetType()} type3: {propertyType.Name} {field?.Name}");
                    if (propertyType.GetCustomAttributes(true)
                           .Any(x => x.GetType() == typeof(NotMappedAttribute))) continue;
                    tableColumns.Add(new ColumnMetadata
                    {
                        ColumnName = propertyType.Name,
                        DataType = propertyType.Name == "id" ? "uniqueidentifier" : field.Name,
                        IsNull = field != null,
                        Type = field ?? propertyType.GetType(),
                        IsList = isList
                    });
                }
            }
            else
            {
                foreach (var propertyType in entityType.GetProperties())
                {
                    tableColumns.Add(new ColumnMetadata
                    {
                        ColumnName = propertyType.GetColumnName(),
                        DataType = propertyType.GetRelationalTypeMapping().ClrType.Name,
                        IsNull = false,
                        Type = propertyType.GetRelationalTypeMapping().ClrType,
                        IsList = false
                    });
                    //Console.WriteLine($"columnMetadata info {JObject.FromObject(columnMetadata)}");
                }
            }
            return tableColumns;
        }
    }
}
