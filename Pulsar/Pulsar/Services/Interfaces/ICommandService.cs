// [Path]: Pulsar/Services/Interfaces/ICommandService.cs
using System.Threading.Tasks;

namespace Pulsar.Services.Interfaces
{
    public interface ICommandService
    {
        // 使用完全限定名 Pulsar.Models.GridItem，防止任何歧义
        Task ExecuteAsync(Pulsar.Models.GridItem item);
    }
}