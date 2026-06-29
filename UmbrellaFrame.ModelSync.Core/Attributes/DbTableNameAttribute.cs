using System;
using System.Collections.Generic;
using System.Text;

namespace UmbrellaFrame.ModelSync.Core
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class DbTableNameAttribute : Attribute
    {
        public string TableName { get; }

        public DbTableNameAttribute(string tableName)
        {
            TableName = tableName;
        }
    }
}
