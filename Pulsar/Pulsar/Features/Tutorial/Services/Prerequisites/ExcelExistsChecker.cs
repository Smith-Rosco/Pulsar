using System;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Pulsar.Features.Tutorial.Services.Prerequisites
{
    public sealed class ExcelExistsChecker : IPrerequisiteChecker
    {
        public string Id => "ExcelExists";

        public string DisplayNameKey => "Prerequisite.ExcelExists";

        public PrerequisiteSeverity Severity => PrerequisiteSeverity.Required;

        public Task<PrerequisiteResult> CheckAsync()
        {
            var found = false;
            try
            {
                using var key = Registry.ClassesRoot.OpenSubKey("Excel.Application\\CurVer");
                found = key != null;
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
                Details = found ? null : "Microsoft Excel is not detected on this system."
            });
        }
    }
}
