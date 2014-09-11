using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace DTS.DbDescriptionUpdater
{
    /// <summary>
    /// Db Description Updater, seed database with model property descriptions. Descriptions will appear in Extended Properties of the column.
    /// </summary>
    /// <remarks>
    /// DbDescription updater does not require migraitons. It runs when update-database is called.
    /// Install: 
    /// 1. Add reference to DTS.DbDescriptinUpdater
    /// 2. Add DbDescriptionUdater to Seed method in  Migrations/Configuration.cs
    /// DbDescriptionUpdater<ContextClass> updater = new DbDescriptionUpdater<ContextClass>(context);
    /// updater.UpdateDatabaseDescriptions();
    /// 
    /// Use: 
    /// 1. Decorate model with DbTableMetaAttribute
    /// [DbTableMeta(Description = "Storage for persons. Has many addresses")]
    /// public class Person { }
    /// 
    /// 2. Decorate model properties with the DbColumnMetaAttribute
    /// [DbColumnMeta(Description = "Person's first name")]
    /// public string FirstName { get; set; }
    ///
    /// Note -- [DbColumnMeta(Description = "Person's first name")]Virtual properties are skipped
    /// </remarks>
    /// <typeparam name="TContext"></typeparam>
    public class DbDescriptionUpdater<TContext> where TContext : System.Data.Entity.DbContext
    {
        public DbDescriptionUpdater(TContext context)
        {
            this.context = context;
        }

        Type contextType;
        TContext context;
        DbTransaction transaction;
        public void UpdateDatabaseDescriptions()
        {
            contextType = typeof(TContext);
            
            var props = contextType.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            transaction = null;
            try
            {
                context.Database.Connection.Open();
                transaction = context.Database.Connection.BeginTransaction();
                foreach (var prop in props)
                {
                    if (prop.PropertyType.InheritsOrImplements((typeof(DbSet<>))))
                    {
                        var tableType = prop.PropertyType.GetGenericArguments()[0];
                        SetTableDescriptions(tableType);
                    }
                }
                transaction.Commit();
            }
            catch
            {
                if (transaction != null)
                    transaction.Rollback();
                throw;
            }
            finally
            {
                if (context.Database.Connection.State == System.Data.ConnectionState.Open)
                    context.Database.Connection.Close();
            }
        }

        private void SetTableDescriptions(Type tableType)
        {
            string fullTableName = context.GetTableName(tableType);
            
            Regex regex = new Regex(@"(\[\w+\]\.)?\[(?<table>.*)\]");
            
            Match match = regex.Match(fullTableName);
            
            string tableName;
            
            if (match.Success)
            {
                tableName = match.Groups["table"].Value;
            }
            else
            {
                tableName = fullTableName;
            }

            var tableAttrs = tableType.GetCustomAttributes(typeof(TableAttribute), false);
            
            if (tableAttrs.Length > 0)
            {
                tableName = ((TableAttribute)tableAttrs[0]).Name;
            }


            var tableMetaAttrs = tableType.GetCustomAttributes(typeof(DbTableMetaAttribute), false);
            if (tableMetaAttrs.Length > 0)
            {
                // add meta
                var description = ((DbTableMetaAttribute)tableMetaAttrs[0]).Description;
                SetTableDescription(tableName, description);
            }

            foreach (var prop in tableType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                // nothing prevents users from decorating virtual properties with the DbColumnMeta
                // but virtual properties do not exist in the DB. So we skip over them
                if (!prop.GetGetMethod().IsVirtual)
                {
                    if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
                    {
                        continue;
                    }
                    var attrs = prop.GetCustomAttributes(typeof(DbColumnMetaAttribute), false);
                    if (attrs.Length > 0)
                    {
                        SetColumnDescription(tableName, prop.Name, ((DbColumnMetaAttribute)attrs[0]).Description);
                    }
                }
            }
        }


        private void SetTableDescription(string tableName, string description)
        {
            string strGetDesc = "select [value] from fn_listextendedproperty('MS_Description','schema','dbo','table', null, null,null) where objname = N'" + tableName + "';";
            var prevDesc = RunSqlScalar(strGetDesc);
            RunSql(
                ExtendedTablePropertyQuery(prevDesc != null),
                new SqlParameter("@table", tableName),
                new SqlParameter("@desc", description));
        }

        private void SetColumnDescription(string tableName, string columnName, string description)
        {
            string strGetDesc = "select [value] from fn_listextendedproperty('MS_Description','schema','dbo','table',N'" + tableName + "','column',null) where objname = N'" + columnName + "';";
            var prevDesc = RunSqlScalar(strGetDesc);
            RunSql(
                ExtendedColumnPropertyQuery(prevDesc != null),
                new SqlParameter("@table", tableName),
                new SqlParameter("@column", columnName),
                new SqlParameter("@desc", description));
        }

        DbCommand CreateCommand(string cmdText, params SqlParameter[] parameters)
        {
            var cmd = context.Database.Connection.CreateCommand();
            cmd.CommandText = cmdText;
            cmd.Transaction = transaction;
            foreach (var p in parameters)
                cmd.Parameters.Add(p);
            return cmd;
        }

        void RunSql(string cmdText, params SqlParameter[] parameters)
        {
            var cmd = CreateCommand(cmdText, parameters);
            cmd.ExecuteNonQuery();
        }
        
        object RunSqlScalar(string cmdText, params SqlParameter[] parameters)
        {
            var cmd = CreateCommand(cmdText, parameters);
            return cmd.ExecuteScalar();
        }


        private string ExtendedTablePropertyQuery(bool isUpdate = false)
        {
            string sproc = !isUpdate ? "sp_addextendedproperty" : "sp_updateextendedproperty";
            string query = string.Format(@"EXEC {0}
                    @name = N'MS_Description', 
                    @value = @desc, 
                    @level0type = N'Schema', 
                    @level0name = 'dbo', 
                    @level1type = N'Table',  
                    @level1name = @table, 
                    @level2type = null, 
                    @level2name = null", sproc);
            return query;
        }
        
        private string ExtendedColumnPropertyQuery(bool isUpdate = false)
        {
            string sproc = !isUpdate ? "sp_addextendedproperty" : "sp_updateextendedproperty";
            string query = string.Format(@"EXEC {0} 
                    @name = N'MS_Description', 
                    @value = @desc, 
                    @level0type = N'Schema', 
                    @level0name = 'dbo', 
                    @level1type = N'Table', 
                    @level1name = @table, 
                    @level2type = N'Column', 
                    @level2name = @column", sproc);
            return query;
        }
    }

    public static class ReflectionUtil
    {

        public static bool InheritsOrImplements(this Type child, Type parent)
        {
            parent = ResolveGenericTypeDefinition(parent);

            var currentChild = child.IsGenericType ? child.GetGenericTypeDefinition() : child;

            while (currentChild != typeof(object))
            {
                if (parent == currentChild || HasAnyInterfaces(parent, currentChild))
                {
                    return true;
                }

                currentChild = currentChild.BaseType != null && currentChild.BaseType.IsGenericType 
                    ? currentChild.BaseType.GetGenericTypeDefinition()
                    : currentChild.BaseType;

                if (currentChild == null)
                {
                    return false;
                }
            }
            return false;
        }

        private static bool HasAnyInterfaces(Type parent, Type child)
        {
            return child.GetInterfaces()
                .Any(childInterface =>
                {
                    var currentInterface = childInterface.IsGenericType
                        ? childInterface.GetGenericTypeDefinition()
                        : childInterface;

                    return currentInterface == parent;
                });
        }

        private static Type ResolveGenericTypeDefinition(Type parent)
        {
            var shouldUseGenericType = true;
            if (parent.IsGenericType && parent.GetGenericTypeDefinition() != parent)
                shouldUseGenericType = false;

            if (parent.IsGenericType && shouldUseGenericType)
                parent = parent.GetGenericTypeDefinition();
            return parent;
        }

    }

    public static class ContextExtensions
    {
        public static string GetTableName(this DbContext context, Type tableType)
        {
            MethodInfo method = typeof(ContextExtensions).GetMethod("GetTableName", new Type[] { typeof(DbContext) })
                             .MakeGenericMethod(new Type[] { tableType });
            return (string)method.Invoke(context, new object[] { context });
        }
        public static string GetTableName<T>(this DbContext context) where T : class
        {
            ObjectContext objectContext = ((IObjectContextAdapter)context).ObjectContext;

            return objectContext.GetTableName<T>();
        }

        public static string GetTableName<T>(this ObjectContext context) where T : class
        {
            string sql = context.CreateObjectSet<T>().ToTraceString();
            Regex regex = new Regex("FROM (?<table>.*) AS");
            Match match = regex.Match(sql);

            string table = match.Groups["table"].Value;
            return table;
        }
    }
}
