namespace UmbrellaFrame.ModelSync.Core.SqlGeneration
{
    public sealed class ModelSyncSqlParameter
    {
        public ModelSyncSqlParameter(string name, object value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        public object Value { get; }
    }
}
