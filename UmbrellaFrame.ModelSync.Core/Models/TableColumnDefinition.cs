namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>Parsed table column definition from a CREATE TABLE script.</summary>
    public sealed class TableColumnDefinition
    {
        public string Schema { get; set; } = string.Empty;
        public string Table { get; set; } = string.Empty;
        public string Column { get; set; } = string.Empty;
        public string Definition { get; set; } = string.Empty;
    }
}
