
using System;
using UmbrellaFrame.ModelSync.Core.Resources;

namespace UmbrellaFrame.ModelSync.Core
{
    public abstract class DbColumnTypeAttribute : Attribute
    {
        protected const int MaxVarcharLength = 65535;

        public string ColumnType { get; }
        public string Length { get; }

        protected DbColumnTypeAttribute(string columnType)
        {
            ColumnType = columnType;
            Length = string.Empty;
        }
        protected DbColumnTypeAttribute(string columnType, string? length = null)
        {
            if (!IsValidLengthForType(columnType, length))
            {
                throw new ArgumentException(CoreResources.Get("DbColumnType_InvalidLength", length ?? "null", columnType));
            }

            ColumnType = columnType;
            Length = length ?? string.Empty;
        }

        private bool IsValidLengthForType(string columnType, string? length)
        {
            if (columnType == "CHAR" || columnType == "NCHAR" || columnType == "VARCHAR" || columnType == "NVARCHAR" || columnType == "TEXT" || columnType == "TINYTEXT")
            {
                return IsValidLength(length ?? string.Empty);
            }

            return true;
        }

        private bool IsValidLength(string? length)
        {
            if (string.IsNullOrEmpty(length)) return true;

            if (string.Equals(length, "MAX", StringComparison.OrdinalIgnoreCase)) return true;

            if (int.TryParse(length, out int parsedLength))
            {
                return parsedLength > 0 && parsedLength <= MaxVarcharLength;
            }

            return false;
        }

        public abstract string GetColumnType();
    }

}
