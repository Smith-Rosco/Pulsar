// [Path]: Pulsar/Pulsar/Core/Plugin/Metadata/ValidationRule.cs

namespace Pulsar.Core.Plugin.Metadata
{
    /// <summary>
    /// 验证规则基类
    /// </summary>
    public abstract class ValidationRule
    {
        /// <summary>
        /// 验证值是否符合规则
        /// </summary>
        /// <param name="value">待验证的值</param>
        /// <param name="errorMessage">错误消息 (验证失败时输出)</param>
        /// <returns>验证是否通过</returns>
        public abstract bool Validate(object? value, out string errorMessage);
    }

    /// <summary>
    /// 范围验证规则 (用于数值类型)
    /// </summary>
    public class RangeValidator : ValidationRule
    {
        public int Min { get; }
        public int Max { get; }

        public RangeValidator(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public override bool Validate(object? value, out string errorMessage)
        {
            if (value is int intValue)
            {
                if (intValue < Min || intValue > Max)
                {
                    errorMessage = $"Value must be between {Min} and {Max}";
                    return false;
                }
            }
            else if (value is long longValue)
            {
                if (longValue < Min || longValue > Max)
                {
                    errorMessage = $"Value must be between {Min} and {Max}";
                    return false;
                }
            }
            else
            {
                errorMessage = "Value must be a number";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// 正则表达式验证规则
    /// </summary>
    public class RegexValidator : ValidationRule
    {
        public string Pattern { get; }
        public string ErrorMessage { get; }

        public RegexValidator(string pattern, string errorMessage)
        {
            Pattern = pattern;
            ErrorMessage = errorMessage;
        }

        public override bool Validate(object? value, out string errorMessage)
        {
            if (value is string strValue)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(strValue, Pattern))
                {
                    errorMessage = ErrorMessage;
                    return false;
                }
            }
            else
            {
                errorMessage = "Value must be a string";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// 非空验证规则
    /// </summary>
    public class RequiredValidator : ValidationRule
    {
        public override bool Validate(object? value, out string errorMessage)
        {
            if (value == null || (value is string str && string.IsNullOrWhiteSpace(str)))
            {
                errorMessage = "This field is required";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
