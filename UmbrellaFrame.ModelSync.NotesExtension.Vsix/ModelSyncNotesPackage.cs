using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;

namespace UmbrellaFrame.ModelSync.NotesExtension.Vsix
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    public sealed class ModelSyncNotesPackage : AsyncPackage
    {
        public const string PackageGuidString = "d55b63ea-0622-4a6a-b9a0-6e5b19cc92e5";

        protected override System.Threading.Tasks.Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            return base.InitializeAsync(cancellationToken, progress);
        }
    }
}
