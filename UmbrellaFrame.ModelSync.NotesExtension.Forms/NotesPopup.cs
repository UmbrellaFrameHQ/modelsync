using System;
using System.Windows.Forms;
using UmbrellaFrame.ModelSync.NotesExtension.Services;

namespace UmbrellaFrame.ModelSync.NotesExtension.Forms
{
    public static class NotesPopup
    {
        public static DialogResult Show(
            IWin32Window? owner,
            ModelNotesService notesService,
            Type modelType,
            string propertyName)
        {
            using (var form = new ModelPropertyNotesForm(notesService, modelType, propertyName))
            {
                return form.ShowDialog(owner);
            }
        }

        public static DialogResult Show(
            IWin32Window? owner,
            ModelNotesService notesService,
            string modelName,
            string propertyName)
        {
            using (var form = new ModelPropertyNotesForm(notesService, modelName, propertyName))
            {
                return form.ShowDialog(owner);
            }
        }

        public static DialogResult ShowForKey(
            IWin32Window? owner,
            ModelNotesService notesService,
            string noteKey,
            string displayTitle)
        {
            using (var form = ModelPropertyNotesForm.ForNoteKey(notesService, noteKey, displayTitle))
            {
                return form.ShowDialog(owner);
            }
        }
    }
}
