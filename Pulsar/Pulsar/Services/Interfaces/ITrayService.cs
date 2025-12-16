using System;

namespace Pulsar.Services.Interfaces
{
    public interface ITrayService : IDisposable
    {
        /// <summary>
        /// 初始化托盘图标并显示
        /// </summary>
        void Initialize();
    }
}