// [Path]: Pulsar/Pulsar/Logging/SensitiveDataMaskingEnricher.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace Pulsar.Logging
{
    /// <summary>
    /// Serilog Enricher - 自动脱敏日志中的敏感数据
    /// 支持密码、Token、API Key、信用卡号等敏感信息的自动识别和掩码
    /// </summary>
    public class SensitiveDataMaskingEnricher : ILogEventEnricher
    {
        private static readonly string MaskValue = "***REDACTED***";
        
        // 敏感字段名称模式（不区分大小写）
        private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "password", "pwd", "passwd", "secret", "token", "apikey", "api_key", 
            "accesstoken", "access_token", "refreshtoken", "refresh_token",
            "authorization", "auth", "credential", "credentials", "privatekey", 
            "private_key", "connectionstring", "connection_string", "sessionid",
            "session_id", "cookie", "ssn", "social_security", "creditcard", 
            "credit_card", "cvv", "pin", "otp", "bearer"
        };

        // 正则表达式模式（用于检测值中的敏感数据）
        private static readonly List<Regex> SensitivePatterns = new()
        {
            // API Keys (通用格式: 32-64位字母数字)
            new Regex(@"\b[A-Za-z0-9_\-]{32,64}\b", RegexOptions.Compiled),
            
            // JWT Tokens (格式: xxx.yyy.zzz)
            new Regex(@"\beyJ[A-Za-z0-9_\-]+\.eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\b", RegexOptions.Compiled),
            
            // Bearer Tokens
            new Regex(@"Bearer\s+[A-Za-z0-9_\-\.]+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            
            // 信用卡号 (简化版，支持常见格式)
            new Regex(@"\b\d{4}[\s\-]?\d{4}[\s\-]?\d{4}[\s\-]?\d{4}\b", RegexOptions.Compiled),
            
            // 社会安全号 (SSN: 123-45-6789)
            new Regex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled),
            
            // Email (部分脱敏)
            new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled),
            
            // 连接字符串中的密码
            new Regex(@"(password|pwd)\s*=\s*[^;]+", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        };

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            // 1. 脱敏消息模板中的敏感数据
            if (logEvent.MessageTemplate?.Text != null)
            {
                var maskedMessage = MaskSensitiveData(logEvent.MessageTemplate.Text);
                if (maskedMessage != logEvent.MessageTemplate.Text)
                {
                    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("OriginalMessageMasked", true));
                }
            }

            // 2. 脱敏属性值
            var propertiesToMask = new List<(string Key, LogEventPropertyValue Value)>();
            
            foreach (var property in logEvent.Properties)
            {
                if (ShouldMaskProperty(property.Key, property.Value))
                {
                    propertiesToMask.Add((property.Key, property.Value));
                }
            }

            foreach (var (key, value) in propertiesToMask)
            {
                var maskedValue = MaskPropertyValue(value, propertyFactory);
                logEvent.RemovePropertyIfPresent(key);
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(key, maskedValue));
            }

            // 3. 脱敏异常消息中的敏感数据
            if (logEvent.Exception != null)
            {
                var maskedExceptionMessage = MaskSensitiveData(logEvent.Exception.Message);
                if (maskedExceptionMessage != logEvent.Exception.Message)
                {
                    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("ExceptionMessageMasked", true));
                }
            }
        }

        /// <summary>
        /// 判断属性是否需要脱敏
        /// </summary>
        private bool ShouldMaskProperty(string key, LogEventPropertyValue value)
        {
            // 检查键名是否包含敏感关键词
            if (SensitiveKeys.Any(sensitiveKey => key.Contains(sensitiveKey, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // 检查值是否包含敏感模式
            if (value is ScalarValue scalarValue && scalarValue.Value is string stringValue)
            {
                return ContainsSensitivePattern(stringValue);
            }

            return false;
        }

        /// <summary>
        /// 检查字符串是否包含敏感模式
        /// </summary>
        private bool ContainsSensitivePattern(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length < 8)
            {
                return false;
            }

            // 避免误判：跳过文件路径、URL路径等
            if (text.Contains(":\\") || text.StartsWith("/") || text.StartsWith("http"))
            {
                return false;
            }

            return SensitivePatterns.Any(pattern => pattern.IsMatch(text));
        }

        /// <summary>
        /// 脱敏属性值
        /// </summary>
        private object MaskPropertyValue(LogEventPropertyValue value, ILogEventPropertyFactory propertyFactory)
        {
            if (value is ScalarValue scalarValue)
            {
                if (scalarValue.Value is string stringValue)
                {
                    return MaskSensitiveData(stringValue);
                }
                return MaskValue;
            }

            if (value is StructureValue structureValue)
            {
                // 递归脱敏结构化数据
                var maskedProperties = structureValue.Properties.Select(p =>
                {
                    var maskedValue = ShouldMaskProperty(p.Name, p.Value)
                        ? propertyFactory.CreateProperty(p.Name, MaskValue).Value
                        : p.Value;
                    return new LogEventProperty(p.Name, maskedValue);
                }).ToList();

                return new StructureValue(maskedProperties, structureValue.TypeTag);
            }

            if (value is SequenceValue sequenceValue)
            {
                // 递归脱敏数组数据
                var maskedElements = sequenceValue.Elements.Select(e =>
                    MaskPropertyValue(e, propertyFactory) as LogEventPropertyValue ?? e
                ).ToList();

                return new SequenceValue(maskedElements);
            }

            return MaskValue;
        }

        /// <summary>
        /// 使用正则表达式脱敏文本中的敏感数据
        /// </summary>
        private string MaskSensitiveData(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            var maskedText = text;

            // 应用所有敏感模式
            foreach (var pattern in SensitivePatterns)
            {
                maskedText = pattern.Replace(maskedText, match =>
                {
                    // 保留前后各2个字符用于调试（如果长度足够）
                    if (match.Value.Length <= 8)
                    {
                        return MaskValue;
                    }

                    var prefix = match.Value.Substring(0, 2);
                    var suffix = match.Value.Substring(match.Value.Length - 2);
                    return $"{prefix}***{suffix}";
                });
            }

            return maskedText;
        }
    }
}
