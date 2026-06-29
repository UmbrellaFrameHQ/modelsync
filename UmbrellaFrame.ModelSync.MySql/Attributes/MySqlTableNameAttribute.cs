using System;
using System.Collections.Generic;
using System.Text;
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.MySql
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class MySqlTableNameAttribute : DbTableNameAttribute
    {
        public new string TableName { get; }

        public MySqlTableNameAttribute(string tableName) : base (tableName)
        {
            TableName = tableName;
        }
    }
}
