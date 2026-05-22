using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace UmbrellaFrame.ModelSync.NotesExtension.Vsix.Editor
{
    internal static class ModelSyncNotesAdornmentLayerDefinition
    {
        public const string LayerName = "ModelSyncNotesAdornmentLayer";

        [Export(typeof(AdornmentLayerDefinition))]
        [Name(LayerName)]
        [Order(After = PredefinedAdornmentLayers.Selection, Before = PredefinedAdornmentLayers.Text)]
#pragma warning disable 0649
        public static AdornmentLayerDefinition? LayerDefinition;
#pragma warning restore 0649
    }
}
