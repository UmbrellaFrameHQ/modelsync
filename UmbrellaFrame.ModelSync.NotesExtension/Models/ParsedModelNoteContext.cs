namespace UmbrellaFrame.ModelSync.NotesExtension.Models
{
    public sealed class ParsedModelNoteContext
    {
        public ParsedModelNoteContext(
            int lineNumber,
            string modelName,
            string propertyName,
            string noteKey,
            string displayName,
            string legacyNoteKey)
        {
            LineNumber = lineNumber;
            ModelName = modelName;
            PropertyName = propertyName;
            NoteKey = noteKey;
            DisplayName = displayName;
            LegacyNoteKey = legacyNoteKey;
        }

        public int LineNumber { get; }

        public string ModelName { get; }

        public string PropertyName { get; }

        public string NoteKey { get; }

        public string DisplayName { get; }

        public string LegacyNoteKey { get; }
    }
}
