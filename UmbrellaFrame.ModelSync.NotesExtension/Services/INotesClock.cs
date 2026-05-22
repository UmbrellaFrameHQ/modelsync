using System;

namespace UmbrellaFrame.ModelSync.NotesExtension.Services
{
    public interface INotesClock
    {
        DateTimeOffset UtcNow { get; }
    }
}
