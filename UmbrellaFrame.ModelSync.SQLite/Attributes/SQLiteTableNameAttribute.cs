using System;
using System.Collections.Generic;
using System.Text;
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.SQLite
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class SQLiteTableNameAttribute : DbTableNameAttribute
    {
        public new string TableName { get; }

        public SQLiteTableNameAttribute(string tableName) : base(tableName)
        {
            TableName = tableName;
        }
    }
}
