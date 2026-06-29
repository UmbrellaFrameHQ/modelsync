namespace UmbrellaFrame.ModelSync.Core
{
    public enum ModelSyncPlanPhase
    {
        InfrastructurePrerequisites = 1,
        CreateSchemas = 2,
        CreateTables = 3,
        AddColumns = 4,
        AddDefaultConstraints = 5,
        AddCheckConstraints = 6,
        AddUniqueConstraints = 7,
        AddIndexes = 8,
        AddForeignKeys = 9,
        ApplyScripts = 10,
        RecordHistory = 11,
        BlockedReview = 90
    }
}
