using System;

using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.SqlServer;

namespace UmbrellaFrame.ModelSync.SqlServer
{
    [AttributeUsage(AttributeTargets.Property)]
    public class SqlServerColumnTypeAttribute : DbColumnTypeAttribute
    {
        private new SqlServerColumnType ColumnType { get; }
        public new string Length { get; }

        public SqlServerColumnTypeAttribute(SqlServerColumnType columnType) : base(columnType.ToString())
        {
            ColumnType = columnType;
        }

        public SqlServerColumnTypeAttribute(SqlServerColumnType columnType, string length = null) : base(columnType.ToString(), length)
        {
            ColumnType = columnType;
            Length = length;
        }

        public override string GetColumnType()
        {
            return string.IsNullOrEmpty(Length) ? ColumnType.ToString() : $"{ColumnType}({Length})";
        }
    }
}
