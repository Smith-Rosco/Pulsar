using System;
using System.Dynamic;
using System.Reflection;
using FluentAssertions;
using Pulsar.Plugins.Extensions.VbaRunner;
using Xunit;

namespace Pulsar.Tests.Plugin
{
    /// <summary>
    /// VbaModuleInjector.EnsureVbaProjectIsUsable WPS 空壳守护（repositioning 6.4 治本回归）：
    /// WPS 未装 VBA 组件时 VBProject 返回空壳对象（VBComponents null / Count==0），
    /// 此前用户只看到「宏没反应」，现在应抛出含 WPS 安装指引的可读错误；
    /// 探测本身异常（dynamic 绑定失败 / getter 抛出）时保守放行，让真实调用暴露错误。
    /// </summary>
    public class VbaModuleInjectorWpsGuardTests
    {
        private static Exception? InvokeAndCapture(object? vbProject)
        {
            var method = typeof(VbaModuleInjector).GetMethod(
                "EnsureVbaProjectIsUsable",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull("EnsureVbaProjectIsUsable 方法签名被改动，请同步更新测试");
            try { method!.Invoke(null, new[] { vbProject }); return null; }
            catch (TargetInvocationException ex) { return ex.InnerException!; }
        }

        [Fact]
        public void NullVBProject_ShouldThrow_WithWpsGuidance()
        {
            var ex = InvokeAndCapture(null);

            ex.Should().BeOfType<InvalidOperationException>();
            ex!.Message.Should().Contain("WPS");
            ex.Message.Should().Contain("Trust access");
        }

        [Fact]
        public void NullVBComponents_ShouldThrow_WithWpsGuidance()
        {
            object project = new ExpandoObject();
            ((dynamic)project).VBComponents = null;

            var ex = InvokeAndCapture(project);

            ex.Should().BeOfType<InvalidOperationException>();
            ex!.Message.Should().Contain("WPS");
        }

        [Fact]
        public void ZeroComponentCount_ShouldThrow_WithWpsGuidance()
        {
            object project = new ExpandoObject();
            dynamic shellComponents = new ExpandoObject();
            shellComponents.Count = 0;
            ((dynamic)project).VBComponents = shellComponents;

            var ex = InvokeAndCapture(project);

            ex.Should().BeOfType<InvalidOperationException>();
            ex!.Message.Should().Contain("WPS");
        }

        [Fact]
        public void NonZeroComponentCount_ShouldNotThrow()
        {
            object project = new ExpandoObject();
            dynamic components = new ExpandoObject();
            components.Count = 2;
            ((dynamic)project).VBComponents = components;

            var ex = InvokeAndCapture(project);

            ex.Should().BeNull("非空 VBProject 应视为可用");
        }

        [Fact]
        public void MissingVBComponentsMember_ShouldAssumeUsable()
        {
            // dynamic 绑定失败（对象没有 VBComponents 成员）≠ 空壳 → 保守放行
            object shell = new ExpandoObject();

            var ex = InvokeAndCapture(shell);

            ex.Should().BeNull("探测失败应放行，由真实调用暴露错误");
        }

        [Fact]
        public void ProbeGetterFailure_ShouldAssumeUsable_LetRealCallSurfaceError()
        {
            var project = new ThrowingComponentsProject();

            var ex = InvokeAndCapture(project);

            ex.Should().BeNull("探测异常应保守放行，由真实调用暴露错误");
        }

        // public nested：VbaModuleInjector（Pulsar 程序集）经 dynamic 绑定访问该属性，
        // 类型必须跨程序集可见，否则绑定失败会走「missing member」分支而非「getter 抛出」分支。
        public sealed class ThrowingComponentsProject
        {
            // ReSharper disable once UnusedMember.Global — 供 dynamic 绑定调用
            public object VBComponents => throw new InvalidOperationException("probe failure");
        }
    }
}
