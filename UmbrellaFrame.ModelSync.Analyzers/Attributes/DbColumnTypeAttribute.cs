
using System;
using UmbrellaFrame.ModelSync.Core.Analyzers.Resources;

namespace UmbrellaFrame.ModelSync.Core.Analyzers.Attributes
{
    public abstract class DbColumnTypeAttribute : Attribute
    {
        protected const int MaxVarcharLength = 65535;

        public string Length { get; }
        public abstract string DatabaseType { get; } // Her veritabanı katmanında uygulanacak

        protected DbColumnTypeAttribute(string length = null)
        {
            Length = length;
            ValidateLength();
        }

        private void ValidateLength()
        {
            if (DatabaseType == "VARCHAR" || DatabaseType == "NVARCHAR" || DatabaseType == "TEXT")
            {
                if (!IsValidLength(Length))
                {
                    throw new ArgumentException(
                        AnalyzerResources.GetString("Attr_InvalidLength", DatabaseType));
                }
            }
            else if (Length != null)
            {
                throw new ArgumentException(
                    AnalyzerResources.GetString("Attr_LengthNotSupported", DatabaseType));
            }
        }

        private bool IsValidLength(string length)
        {
            if (string.IsNullOrEmpty(length)) return true;

            if (length.ToUpper() == "MAX") return true;

            if (int.TryParse(length, out int parsedLength))
            {
                return parsedLength > 0 && parsedLength <= MaxVarcharLength;
            }

            return false;
        }
    }
}
