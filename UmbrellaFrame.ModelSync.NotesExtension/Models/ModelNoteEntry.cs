using System;

namespace UmbrellaFrame.ModelSync.NotesExtension.Models
{
    public sealed class ModelNoteEntry
    {
        public string Id { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        public NotesUser CreatedBy { get; set; } = new NotesUser();

        public string Text { get; set; } = string.Empty;

        public DateTimeOffset? UpdatedAt { get; set; }

        public NotesUser? UpdatedBy { get; set; }
    }
}
