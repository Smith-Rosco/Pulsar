using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Pulsar.Services.Tutorial.Prerequisites
{
    public sealed class BrowserExistsChecker : IPrerequisiteChecker
    {
        public string Id => "BrowserExists";

        public string DisplayNameKey => "Prerequisite.BrowserExists";

        public PrerequisiteSeverity Severity => PrerequisiteSeverity.Required;

        public Task<PrerequisiteResult> CheckAsync()
        {
            var foundBrowsers = new List<string>();

            try
            {
                var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];

                foreach (var dir in paths)
                {
                    if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;

                    var chromePath = Path.Combine(dir, "chrome.exe");
                    if (File.Exists(chromePath))
                    {
                        foundBrowsers.Add("Google Chrome");
                    }

                    var edgePath = Path.Combine(dir, "msedge.exe");
                    if (File.Exists(edgePath))
                    {
                        foundBrowsers.Add("Microsoft Edge");
                    }
                }
            }
            catch
            {
            }

            var found = foundBrowsers.Count > 0;
            var details = found
                ? $"Detected: {string.Join(", ", foundBrowsers)}"
                : "No supported browser (Chrome, Edge) found in PATH.";

            return Task.FromResult(new PrerequisiteResult
            {
                Id = Id,
                DisplayNameKey = DisplayNameKey,
                Severity = Severity,
                Status = found ? PrerequisiteStatus.Met : PrerequisiteStatus.NotMet,
                Details = details
            });
        }
    }
}
