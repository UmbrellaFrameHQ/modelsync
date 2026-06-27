namespace UmbrellaFrame.ModelSync.Core
{
    public sealed class DatabaseColumnDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string StoreType { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public bool HasDefault { get; set; }
        public bool HasCheck { get; set; }
    }
}
