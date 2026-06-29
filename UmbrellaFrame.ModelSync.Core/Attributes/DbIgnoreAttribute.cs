using System;

namespace UmbrellaFrame.ModelSync.Core
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class DbIgnoreAttribute : Attribute
    {
    }
}
