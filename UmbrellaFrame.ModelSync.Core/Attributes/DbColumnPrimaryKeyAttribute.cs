using System;
using System.Collections.Generic;
using System.Text;

namespace UmbrellaFrame.ModelSync.Core
{
    public abstract class DbColumnPrimaryKeyAttribute : Attribute
    {
        public bool IsAutoIncrement { get; }

        public DbColumnPrimaryKeyAttribute(bool isAutoIncrement = false)
        {
            IsAutoIncrement = isAutoIncrement;
        }

        public abstract string GetSqlSnippet();
    }
}
