using System;
using System.Linq;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Helpers;

namespace UmbrellaFrame.ModelSync.MySql
{
    [AttributeUsage(AttributeTargets.Property)]
    public class MySqlColumnTypeAttribute : DbColumnTypeAttribute
    {
        public new MySqlColumnType ColumnType { get; }
        public new string Length { get; } = null;
        public new Type AllowedValues { get; }

        public MySqlColumnTypeAttribute(MySqlColumnType columnType) : base(columnType.ToString())
        {
            ColumnType = columnType;
        }

        public MySqlColumnTypeAttribute(MySqlColumnType columnType, Type allowedValues) : base(columnType.ToString())
        {
            ColumnType = columnType;
            AllowedValues = allowedValues;
        }

        public MySqlColumnTypeAttribute(MySqlColumnType columnType, string length = null) : base(columnType.ToString(), length)
        {
            ColumnType = columnType;
            if (ColumnType == MySqlColumnType.ENUM || ColumnType == MySqlColumnType.SET)
            {
                Length = length;
            }
            else
            {
                Length = length;
            }
        }

        public override string GetColumnType()
        {
            if ((ColumnType == MySqlColumnType.ENUM || ColumnType == MySqlColumnType.SET) && AllowedValues != null)
            {
                var enumValues = Enum.GetValues(AllowedValues).Cast<Enum>();
                var values = string.Join(", ", enumValues.Select(v => $"'{v}'"));
                return $"{ColumnType}({values})";
            }
            else
            {
                return string.IsNullOrEmpty(Length) ? ColumnType.ToString() : $"{ColumnType}({Length})";
            }
        }
    }
}
