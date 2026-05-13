using System;
using System.Collections.Generic;
using System.Text;
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.SqlServer
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class SqlServerTableNameAttribute : DbTableNameAttribute
    {
        public new string TableName { get; }

        public SqlServerTableNameAttribute(string tableName) : base(tableName)
        {
            TableName = tableName;
        }
    }
}
