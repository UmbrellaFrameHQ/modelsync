using System;
using System.Collections.Generic;

namespace UmbrellaFrame.ModelSync.Core
{
    public sealed class ModelTableDefinition
    {
        public Type ModelType { get; set; } = typeof(object);
        public string Schema { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public IList<ModelColumnDefinition> Columns { get; } = new List<ModelColumnDefinition>();
    }
}
