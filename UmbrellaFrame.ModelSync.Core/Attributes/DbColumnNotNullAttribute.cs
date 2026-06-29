using System;
using System.Collections.Generic;
using System.Text;

namespace UmbrellaFrame.ModelSync.Core
{
    public abstract class DbColumnNotNullAttribute : Attribute
    {
        public DbColumnNotNullAttribute() { }

        public abstract string GetSqlSnippet();
    }
}
