using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace UmbrellaFrame.ModelSync.NotesExtension.Vsix.Editor
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("CSharp")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class ModelSyncNotesAdornmentProvider : IWpfTextViewCreationListener
    {
        public void TextViewCreated(IWpfTextView textView)
        {
            new ModelSyncNotesAdornment(textView);
        }
    }
}
