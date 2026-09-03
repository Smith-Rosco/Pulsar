using System;

namespace Pulsar.Features.Tutorial.Models
{
    /// <summary>
    /// 首次启动向导的"上手场景"选项（由 <see cref="TutorialScenario"/> 驱动）。
    /// 展示顺序由 WorkbenchPillarCatalog 决定：办公自动化支柱场景在前，
    /// 通用/系统演示场景（如记事本）在后。
    /// </summary>
    public sealed class OnboardingUsageOption
    {
        public required TutorialScenario Scenario { get; init; }

        /// <summary>本地化标题（来自 Scenario.TitleKey）。</summary>
        public required string Title { get; init; }

        /// <summary>本地化描述（来自 Scenario.DescriptionKey）。</summary>
        public required string Description { get; init; }

        public override string ToString() => Title ?? Scenario.Id ?? string.Empty;
    }
}
