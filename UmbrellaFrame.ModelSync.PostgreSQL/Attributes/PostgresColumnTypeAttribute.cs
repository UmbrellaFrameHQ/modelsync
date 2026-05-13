using System;

using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.PostgreSQL
{
    [AttributeUsage(AttributeTargets.Property)]
    public class PostgresColumnTypeAttribute : DbColumnTypeAttribute
    {
        private new PostgresColumnType ColumnType { get; }
        public new string Length { get; } = null;

        public PostgresColumnTypeAttribute(PostgresColumnType columnType) : base(MapTypeName(columnType))
        {
            ColumnType = columnType;
        }

        public PostgresColumnTypeAttribute(PostgresColumnType columnType, string length = null) : base(MapTypeName(columnType), length)
        {
            ColumnType = columnType;
            Length = length;
        }

        public override string GetColumnType()
        {
            var typeName = MapTypeName(ColumnType);
            return string.IsNullOrEmpty(Length) ? typeName : $"{typeName}({Length})";
        }

        private static string MapTypeName(PostgresColumnType columnType)
        {
            switch (columnType)
            {
                case PostgresColumnType.DOUBLE_PRECISION: return "double precision";
                case PostgresColumnType.TIMESTAMPTZ:      return "timestamptz";
                default:                                  return columnType.ToString();
            }
        }
    }
}
