using System.Collections.Generic;

namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// 插件配置验证结果
    /// </summary>
    public class PluginConfigValidationResult
    {
        /// <summary>
        /// 配置是否有效
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// 验证错误列表
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// 创建成功的验证结果
        /// </summary>
        public static PluginConfigValidationResult Success()
        {
            return new PluginConfigValidationResult { IsValid = true };
        }

        /// <summary>
        /// 创建失败的验证结果
        /// </summary>
        public static PluginConfigValidationResult Failure(params string[] errors)
        {
            return new PluginConfigValidationResult
            {
                IsValid = false,
                Errors = new List<string>(errors)
            };
        }

        /// <summary>
        /// 添加错误
        /// </summary>
        public void AddError(string error)
        {
            IsValid = false;
            Errors.Add(error);
        }
    }
}
