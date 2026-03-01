// [Path]: Pulsar/Pulsar/Services/Validation/ValidationResult.cs

using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Services.Validation
{
    /// <summary>
    /// 验证结果
    /// </summary>
    public class ValidationResult
    {
        private readonly List<ValidationError> _errors = new();
        private readonly List<ValidationWarning> _warnings = new();
        private readonly List<ValidationInfo> _infos = new();

        /// <summary>
        /// 是否验证通过（无错误）
        /// </summary>
        public bool IsValid => !_errors.Any();

        /// <summary>
        /// 错误列表
        /// </summary>
        public IReadOnlyList<ValidationError> Errors => _errors.AsReadOnly();

        /// <summary>
        /// 警告列表
        /// </summary>
        public IReadOnlyList<ValidationWarning> Warnings => _warnings.AsReadOnly();

        /// <summary>
        /// 信息列表
        /// </summary>
        public IReadOnlyList<ValidationInfo> Infos => _infos.AsReadOnly();

        /// <summary>
        /// 迁移后的配置（如果发生了迁移）
        /// </summary>
        public object? MigratedConfig { get; set; }

        /// <summary>
        /// 添加错误
        /// </summary>
        public void AddError(string message, string? pluginId = null, string? propertyName = null)
        {
            _errors.Add(new ValidationError(message, pluginId, propertyName));
        }

        /// <summary>
        /// 添加多个错误
        /// </summary>
        public void AddErrors(IEnumerable<ValidationError> errors)
        {
            _errors.AddRange(errors);
        }

        /// <summary>
        /// 添加警告
        /// </summary>
        public void AddWarning(string message, string? pluginId = null)
        {
            _warnings.Add(new ValidationWarning(message, pluginId));
        }

        /// <summary>
        /// 添加信息
        /// </summary>
        public void AddInfo(string message)
        {
            _infos.Add(new ValidationInfo(message));
        }

        /// <summary>
        /// 合并另一个验证结果
        /// </summary>
        public void Merge(ValidationResult other)
        {
            _errors.AddRange(other.Errors);
            _warnings.AddRange(other.Warnings);
            _infos.AddRange(other.Infos);
        }
    }

    /// <summary>
    /// 验证错误
    /// </summary>
    public record ValidationError(string Message, string? PluginId = null, string? PropertyName = null);

    /// <summary>
    /// 验证警告
    /// </summary>
    public record ValidationWarning(string Message, string? PluginId = null);

    /// <summary>
    /// 验证信息
    /// </summary>
    public record ValidationInfo(string Message);
}
