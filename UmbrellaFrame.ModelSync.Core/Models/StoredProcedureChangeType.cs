namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>
    /// Describes the difference between the project procedure definition and the live database definition.
    /// </summary>
    public enum StoredProcedureChangeType
    {
        /// <summary>No database change is required.</summary>
        None = 0,

        /// <summary>The procedure does not exist in the database and should be created.</summary>
        Create = 1,

        /// <summary>The procedure exists but differs from the project definition and should be altered.</summary>
        Alter = 2
    }
}
