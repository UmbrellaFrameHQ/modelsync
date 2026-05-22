using System;

namespace UmbrellaFrame.ModelSync.NotesExtension.Services
{
    public sealed class SystemNotesClock : INotesClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
