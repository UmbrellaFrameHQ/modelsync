using System;
using System.Diagnostics;
using System.Text;
using UmbrellaFrame.ModelSync.NotesExtension.Models;
using UmbrellaFrame.ModelSync.NotesExtension.Services;

namespace UmbrellaFrame.ModelSync.NotesExtension.Vsix.Services
{
    internal sealed class VisualStudioNotesUserProvider : INotesUserProvider
    {
        private static readonly object CacheLock = new object();
        private static NotesUser? CachedUser;

        public NotesUser GetCurrentUser()
        {
            lock (CacheLock)
            {
                if (CachedUser != null)
                {
                    return Clone(CachedUser);
                }

                CachedUser = ResolveCurrentUser();
                return Clone(CachedUser);
            }
        }

        private static NotesUser ResolveCurrentUser()
        {
            var gitName = ReadGitConfig("user.name");
            var gitEmail = ReadGitConfig("user.email");

            if (!string.IsNullOrWhiteSpace(gitEmail))
            {
                return new NotesUser
                {
                    Id = gitEmail!,
                    Name = string.IsNullOrWhiteSpace(gitName)
                        ? gitEmail!
                        : RepairText(gitName!),
                    Source = "visualstudio-git"
                };
            }

            var fallback = Environment.UserName;
            return new NotesUser
            {
                Id = fallback,
                Name = RepairText(fallback),
                Source = "visualstudio-fallback"
            };
        }

        private static NotesUser Clone(NotesUser user)
        {
            return new NotesUser
            {
                Id = user.Id,
                Name = user.Name,
                Source = user.Source
            };
        }

        private static string? ReadGitConfig(string key)
        {
            try
            {
                var startInfo = new ProcessStartInfo("git", "config --global " + key)
                {
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    UseShellExecute = false
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return null;
                    }

                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(1000);
                    return process.ExitCode == 0 ? output.Trim() : null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static string RepairText(string value)
        {
            return TurkishTextEncodingRepair.RepairMojibake(value);
        }
    }
}
