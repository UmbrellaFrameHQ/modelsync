using UmbrellaFrame.ModelSync.NotesExtension.Models;

namespace UmbrellaFrame.ModelSync.NotesExtension.Services
{
    public interface INotesUserProvider
    {
        NotesUser GetCurrentUser();
    }
}
