namespace UmbrellaFrame.ModelSync.Core
{
    public enum DbValueGenerationKind
    {
        None = 0,
        Identity = 1,
        AutoIncrement = 2,
        Serial = 3,
        BigSerial = 4,
        RowIdAlias = 5
    }
}
