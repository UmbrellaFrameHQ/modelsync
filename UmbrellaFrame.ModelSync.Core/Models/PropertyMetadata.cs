using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace UmbrellaFrame.ModelSync.Core
{
    public class PropertyMetadata
    {
        public object? Value { get; set; }

        public PropertyInfo PropertyInfo { get; set; } = null!;

        public List<Attribute> Attributes { get; set; } = new List<Attribute>();
    }
}
