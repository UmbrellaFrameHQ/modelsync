namespace UmbrellaFrame.ModelSync.Core
{
    public enum ModelSyncChangeType
    {
        None = 0,
        CreateTable = 1,
        AddColumn = 2,
        AddIndex = 3,
        AddDefaultConstraint = 4,
        AddCheckConstraint = 5,
        AddUniqueConstraint = 6,
        AddForeignKey = 7,
        ApplySqlScript = 8,
        AlterColumnType = 20,
        AlterNullability = 21,
        DropColumn = 22,
        DropTable = 23,
        RenameColumn = 24,
        Unsupported = 99
    }
}
