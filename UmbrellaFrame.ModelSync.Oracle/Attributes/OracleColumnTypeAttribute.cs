using System;
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.Oracle
{
    [AttributeUsage(AttributeTargets.Property)]
    public class OracleColumnTypeAttribute : DbColumnTypeAttribute
    {
        public new OracleColumnType ColumnType { get; }
        public new string Length { get; } = string.Empty;

        public OracleColumnTypeAttribute(OracleColumnType columnType) : base(columnType.ToString())
        {
            ColumnType = columnType;
        }

        public OracleColumnTypeAttribute(OracleColumnType columnType, string length) : base(columnType.ToString(), length)
        {
            ColumnType = columnType;
            Length = length;
        }

        public override string GetColumnType()
            => string.IsNullOrEmpty(Length) ? ColumnType.ToString() : $"{ColumnType}({Length})";
    }
}
