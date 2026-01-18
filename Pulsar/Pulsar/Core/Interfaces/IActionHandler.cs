using System.Threading.Tasks;
using Pulsar.Models;

namespace Pulsar.Core.Interfaces
{
    /// <summary>
    /// 定义一个具体的执行动作处理器
    /// </summary>
    public interface IActionHandler
    {
        /// <summary>
        /// 执行具体的业务逻辑
        /// </summary>
        /// <param name="item">传入的数据模型 (需在实现中强转为具体类型)</param>
        Task ExecuteAsync(GridItemBase item);
    }
}