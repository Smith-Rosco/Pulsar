using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pulsar.Services.Tutorial.Prerequisites
{
    public sealed class BrowserPrerequisiteProvider : IPrerequisiteProvider
    {
        private readonly IReadOnlyList<IPrerequisiteChecker> _checkers;

        public BrowserPrerequisiteProvider()
        {
            _checkers = new List<IPrerequisiteChecker>
            {
                new BrowserExistsChecker()
            };
        }

        public Task<IReadOnlyList<IPrerequisiteChecker>> GetCheckersAsync()
        {
            return Task.FromResult(_checkers);
        }

        public async Task<IReadOnlyList<PrerequisiteResult>> CheckAllAsync()
        {
            var results = new List<PrerequisiteResult>();
            foreach (var checker in _checkers)
            {
                var result = await checker.CheckAsync();
                results.Add(result);
            }
            return results;
        }
    }
}
