using System.Diagnostics;
using System.IO;
using FluentAssertions;
using Pulsar.Services.WindowSwitching;
using Xunit;

namespace Pulsar.Tests.Services
{
    /// <summary>
    /// <see cref="ProcessMetaResolver"/> 的行为契约。
    /// 该类型是 internal，测试项目通过 InternalsVisibleTo 访问（与 WindowInventoryService 同）。
    /// </summary>
    public class ProcessMetaResolverTests
    {
        [Fact]
        public void Resolve_ShouldReturnNull_ForPidZero()
        {
            var resolver = new ProcessMetaResolver();

            resolver.Resolve(0).Should().BeNull();
        }

        [Fact]
        public void Resolve_ShouldReturnNull_ForNonExistentPid()
        {
            var resolver = new ProcessMetaResolver();

            // 极不可能存在的 pid：Windows pid 是 4 的倍数且远小于 int.MaxValue。
            resolver.Resolve(int.MaxValue - 1).Should().BeNull();
        }

        [Fact]
        public void Resolve_ShouldResolveCurrentProcess_NameAndPath()
        {
            var resolver = new ProcessMetaResolver();
            using var current = Process.GetCurrentProcess();

            var meta = resolver.Resolve(current.Id);

            meta.Should().NotBeNull();
            meta!.ProcessName.Should().Be(current.ProcessName);
            meta.ExePath.Should().NotBeEmpty();
            File.Exists(meta.ExePath).Should().BeTrue("解析出的路径应指向真实可执行文件");
        }

        /// <summary>
        /// 进程名必须与 <see cref="Process.ProcessName"/> 一致 —— 进程黑名单、用户规则的
        /// ProcessName 限定、以及 profile 匹配都按这个名字比对，一旦带上 ".exe" 后缀
        /// 或换成别的大小写形式，既有配置会静默失效。
        /// </summary>
        [Fact]
        public void Resolve_ProcessName_ShouldNotIncludeExeExtension()
        {
            var resolver = new ProcessMetaResolver();
            using var current = Process.GetCurrentProcess();

            var meta = resolver.Resolve(current.Id);

            meta!.ProcessName.Should().NotEndWith(".exe");
        }

        [Fact]
        public void Resolve_ShouldMemoizeByPid()
        {
            var resolver = new ProcessMetaResolver();
            using var current = Process.GetCurrentProcess();

            var first = resolver.Resolve(current.Id);
            var second = resolver.Resolve(current.Id);

            // 同一进程的多个窗口共享一次解析结果（同一实例，不只是相等）。
            second.Should().BeSameAs(first);
        }

        [Fact]
        public void Resolve_ShouldBeStable_AcrossRepeatedCalls()
        {
            var resolver = new ProcessMetaResolver();
            int missingPid = int.MaxValue - 1;

            // 负结果同样被记忆化（内部实现细节），对外可观察的契约是结论稳定。
            resolver.Resolve(missingPid).Should().BeNull();
            resolver.Resolve(missingPid).Should().BeNull();
        }
    }
}
