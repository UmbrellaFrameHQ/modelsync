using System;
using UmbrellaFrame.ModelSync.NotesExtension.Models;

namespace UmbrellaFrame.ModelSync.NotesExtension.Services
{
    public interface IModelNotesRepository
    {
        ModelNotesDocument Load();

        void Save(ModelNotesDocument document);

        T Update<T>(Func<ModelNotesDocument, T> update);
    }
}
