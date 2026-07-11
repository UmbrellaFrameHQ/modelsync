using System;
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.Oracle
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class OracleTableNameAttribute : DbTableNameAttribute
    {
        public new string TableName { get; }

        public OracleTableNameAttribute(string tableName) : base(tableName)
        {
            TableName = tableName;
        }
    }
}
