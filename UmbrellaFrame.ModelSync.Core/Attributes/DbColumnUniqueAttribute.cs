using System;
using System.Collections.Generic;
using System.Text;

namespace UmbrellaFrame.ModelSync.Core
{
    public abstract class DbColumnUniqueAttribute : Attribute
    {
        public DbColumnUniqueAttribute() { }

        public abstract string GetSqlSnippet();
    }
}
