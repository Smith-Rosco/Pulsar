using System;
using System.IO;
using System.Threading.Tasks;

namespace Pulsar.Services.Tutorial.Prerequisites
{
    public sealed class VbaSupportChecker : IPrerequisiteChecker
    {
        public string Id => "VbaSupport";

        public string DisplayNameKey => "Prerequisite.VbaSupport";

        public PrerequisiteSeverity Severity => PrerequisiteSeverity.Recommended;

        public Task<PrerequisiteResult> CheckAsync()
        {
            var found = false;
            try
            {
                var officeDirs = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                };

                foreach (var baseDir in officeDirs)
                {
                    if (string.IsNullOrEmpty(baseDir)) continue;

                    var searchPath = Path.Combine(baseDir, "Microsoft Office");
                    if (!Directory.Exists(searchPath)) continue;

                    var vbeFiles = Directory.GetFiles(searchPath, "VBE7.DLL", SearchOption.AllDirectories);
                    if (vbeFiles.Length > 0)
                    {
                        found = true;
                        break;
                    }
                }
            }
            catch
            {
            }

            return Task.FromResult(new PrerequisiteResult
            {
                Id = Id,
                DisplayNameKey = DisplayNameKey,
                Severity = Severity,
                Status = found ? PrerequisiteStatus.Met : PrerequisiteStatus.NotMet,
                Details = found ? null : "VBA support (VBE7.DLL) not found. Tutorial will use text insertion instead."
            });
        }
    }
}
