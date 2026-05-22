using System;
using UmbrellaFrame.ModelSync.NotesExtension.Models;

namespace UmbrellaFrame.ModelSync.NotesExtension.Services
{
    public sealed class StaticNotesUserProvider : INotesUserProvider
    {
        private readonly NotesUser _user;

        public StaticNotesUserProvider(string id, string name, string source = "visualstudio")
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("User id cannot be empty.", nameof(id));
            }

            _user = new NotesUser
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(name) ? id : name,
                Source = string.IsNullOrWhiteSpace(source) ? "visualstudio" : source
            };
        }

        public NotesUser GetCurrentUser()
        {
            return new NotesUser
            {
                Id = _user.Id,
                Name = _user.Name,
                Source = _user.Source
            };
        }
    }
}
