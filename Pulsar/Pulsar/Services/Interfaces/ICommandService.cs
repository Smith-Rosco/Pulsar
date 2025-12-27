using Pulsar.Models;

namespace Pulsar.Services.Interfaces
{
    public interface ICommandService
    {
        // 接口方法签名变更
        Task ExecuteAsync(GridItemBase item);
    }
}