using System.Collections.Generic;

namespace UmbrellaFrame.ModelSync.NotesExtension.Models
{
    public sealed class ModelNotesDocument
    {
        public int SchemaVersion { get; set; } = 1;

        public Dictionary<string, List<ModelNoteEntry>> Notes { get; set; } = new Dictionary<string, List<ModelNoteEntry>>();
    }
}
