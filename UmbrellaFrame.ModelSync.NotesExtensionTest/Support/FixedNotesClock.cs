using System;
using UmbrellaFrame.ModelSync.NotesExtension.Services;

namespace UmbrellaFrame.ModelSync.NotesExtensionTest.Support
{
    internal sealed class FixedNotesClock : INotesClock
    {
        public FixedNotesClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; set; }
    }
}
