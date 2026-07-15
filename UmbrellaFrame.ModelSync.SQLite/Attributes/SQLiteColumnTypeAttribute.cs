
using System;
using System.Linq;
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.SQLite
{
    [AttributeUsage(AttributeTargets.Property)]
    public class SQLiteColumnTypeAttribute : DbColumnTypeAttribute
    {
        private new SQLiteColumnType ColumnType { get; }
        public object? AllowedValues { get; }

        public SQLiteColumnTypeAttribute(SQLiteColumnType columnType) : base(columnType.ToString(), "")
        {
            ColumnType = columnType;
        }

        public SQLiteColumnTypeAttribute(SQLiteColumnType columnType, object allowedValues) : base(columnType.ToString())
        {
            ColumnType = columnType;
            AllowedValues = allowedValues;
        }

        public override string GetColumnType()
        {
            if (ColumnType == SQLiteColumnType.TEXT)
            {
                if (AllowedValues is Type enumType && enumType.IsEnum)
                {
                    var enumValues = Enum.GetValues(enumType).Cast<Enum>();
                    var values = string.Join(", ", enumValues.Select(v => $"'{v}'"));
                    return $"{ColumnType}({values})";
                }
                else if (AllowedValues is string)
                {
                    return $"{ColumnType}('{AllowedValues}')";
                }
                else
                {
                    return string.IsNullOrEmpty(Length) ? ColumnType.ToString() : $"{ColumnType}({Length})";
                }
            }
            else
            {
                return string.IsNullOrEmpty(Length) ? ColumnType.ToString() : $"{ColumnType}({Length})";
            }
        }
    }
}
