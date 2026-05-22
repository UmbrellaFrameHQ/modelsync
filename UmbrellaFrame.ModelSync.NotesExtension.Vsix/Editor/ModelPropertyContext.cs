namespace UmbrellaFrame.ModelSync.NotesExtension.Vsix.Editor
{
    internal sealed class ModelPropertyContext
    {
        public ModelPropertyContext(string modelName, string propertyName, string noteKey, string displayName, string legacyNoteKey)
        {
            ModelName = modelName;
            PropertyName = propertyName;
            NoteKey = noteKey;
            DisplayName = displayName;
            LegacyNoteKey = legacyNoteKey;
        }

        public string ModelName { get; }

        public string PropertyName { get; }

        public string NoteKey { get; }

        public string DisplayName { get; }

        public string LegacyNoteKey { get; }
    }
}
