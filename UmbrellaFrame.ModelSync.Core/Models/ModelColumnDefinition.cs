namespace UmbrellaFrame.ModelSync.Core
{
    public sealed class ModelColumnDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string StoreType { get; set; } = string.Empty;
        public bool IsPrimaryKey { get; set; }
        public DbValueGenerationKind ValueGeneration { get; set; }
        public long? IdentitySeed { get; set; }
        public long? IdentityIncrement { get; set; }
        [System.Obsolete("Use structured ValueGeneration metadata instead.")]
        public string PrimaryKeySqlSnippet { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
        public bool IsUnique { get; set; }
        public bool IsIndexed { get; set; }
        public string IndexName { get; set; } = string.Empty;
        public bool IsUniqueIndex { get; set; }
        public string DefaultSql { get; set; } = string.Empty;
        public string CheckSql { get; set; } = string.Empty;
        public string ForeignKeyColumn { get; set; } = string.Empty;
        public string ForeignKeyTable { get; set; } = string.Empty;
        public string ForeignKeyReferenceColumn { get; set; } = string.Empty;
    }
}
