namespace UmbrellaFrame.ModelSync.Core.Services
{
    public interface IModelSyncTablePolicyResolver
    {
        ModelSyncTableMode Resolve(ModelTableDefinition table);
        ModelSyncTableMode Resolve(string schema, string table);
    }
}
