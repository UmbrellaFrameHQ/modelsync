namespace UmbrellaFrame.ModelSync.Core
{
    public enum MigrationHistoryDecision
    {
        NoHistoryChange = 0,
        RecordFullTargetHash = 1,
        AdoptLegacyHash = 2,
        ManualReviewRequired = 3
    }
}
