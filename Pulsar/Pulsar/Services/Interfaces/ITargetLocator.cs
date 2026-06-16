// [Path]: Pulsar/Pulsar/Services/Interfaces/ITargetLocator.cs

using System.Windows;
using Pulsar.Features.Tutorial.Models;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// 教程目标定位服务接口
    /// 负责查找和定位教程目标元素的屏幕坐标
    /// </summary>
    public interface ITargetLocator
    {
        /// <summary>
        /// 获取目标元素的屏幕坐标
        /// </summary>
        /// <param name="target">目标定义</param>
        /// <returns>目标区域的屏幕坐标,如果找不到则返回 null</returns>
        Rect? GetTargetBounds(TutorialTarget target);

        /// <summary>
        /// 获取托盘图标区域的屏幕坐标
        /// </summary>
        /// <returns>托盘图标区域的屏幕坐标</returns>
        Rect? GetTrayIconBounds();

        /// <summary>
        /// 获取指定窗口的屏幕坐标
        /// </summary>
        /// <param name="windowName">窗口类型名称</param>
        /// <returns>窗口的屏幕坐标,如果找不到则返回 null</returns>
        Rect? GetWindowBounds(string windowName);
    }
}
