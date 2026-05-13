using System;
using System.Collections.Generic;
using System.Text;
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.PostgreSQL
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class PostgresTableName : DbTableNameAttribute
    {
        public new string TableName { get; }

        public PostgresTableName(string tableName) : base(tableName)
        {
            TableName = tableName;
        }
    }
}
