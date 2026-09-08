using System;
using System.Collections.Generic;
using FluentAssertions;
using Pulsar.Core.Plugin.Runtime;
using Xunit;

namespace Pulsar.Tests.Plugin
{
    /// <summary>
    /// PluginRuntimeKernel.ExpandEnvironmentVariablesInArgs 守护（repositioning 6.2 治本回归）：
    /// 槽位参数统一展开环境变量；无变化时按引用原样返回；未定义变量原样保留。
    /// </summary>
    public class PluginRuntimeKernelEnvArgsTests : IDisposable
    {
        private const string VarName = "PULSAR_TEST_ARG_DIR";

        public PluginRuntimeKernelEnvArgsTests()
        {
            Environment.SetEnvironmentVariable(VarName, @"C:\pulsar-test-dir");
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(VarName, null);
        }

        [Fact]
        public void NullArgs_ShouldReturnEmptyReadOnlyDictionary()
        {
            var result = PluginRuntimeKernel.ExpandEnvironmentVariablesInArgs(null);

            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public void EmptyArgs_ShouldReturnSameReference()
        {
            var args = new Dictionary<string, string>().AsReadOnly();

            var result = PluginRuntimeKernel.ExpandEnvironmentVariablesInArgs(args);

            result.Should().BeSameAs(args);
        }

        [Fact]
        public void ArgsWithoutVariables_ShouldReturnSameReference_NoAllocation()
        {
            var args = new Dictionary<string, string> { ["path"] = @"C:\plain\path.exe" }.AsReadOnly();

            var result = PluginRuntimeKernel.ExpandEnvironmentVariablesInArgs(args);

            result.Should().BeSameAs(args);
        }

        [Fact]
        public void ArgsWithVariable_ShouldExpandValue()
        {
            var args = new Dictionary<string, string>
            {
                ["filePath"] = $"%{VarName}%\\data.xlsx",
                ["plain"] = "literal"
            };

            var result = PluginRuntimeKernel.ExpandEnvironmentVariablesInArgs(args);

            result["filePath"].Should().Be(@"C:\pulsar-test-dir\data.xlsx");
            result["plain"].Should().Be("literal");
        }

        [Fact]
        public void ArgsWithVariable_ShouldNotMutateOriginalDictionary()
        {
            var raw = $"%{VarName}%\\data.xlsx";
            var args = new Dictionary<string, string> { ["filePath"] = raw };

            PluginRuntimeKernel.ExpandEnvironmentVariablesInArgs(args);

            args["filePath"].Should().Be(raw, "原字典不得被就地修改");
        }

        [Fact]
        public void UndefinedVariable_ShouldStayUnexpanded()
        {
            var args = new Dictionary<string, string> { ["filePath"] = "%PULSAR_TEST_UNDEFINED_VAR%\\x.xlsx" };

            var result = PluginRuntimeKernel.ExpandEnvironmentVariablesInArgs(args);

            result["filePath"].Should().Be("%PULSAR_TEST_UNDEFINED_VAR%\\x.xlsx");
        }
    }
}
