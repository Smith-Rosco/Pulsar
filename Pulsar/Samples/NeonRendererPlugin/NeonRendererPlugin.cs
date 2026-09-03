using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;
using Pulsar.Core.Rendering;

namespace Pulsar.Samples.NeonRendererPlugin;

/// <summary>
/// Sample renderer contributed through <see cref="IRadialRendererRegistry"/>:
/// a dashed "neon" outer ring plus a soft blur slot highlight. Demonstrates the
/// minimal surface a renderer plugin has to implement and the ui.render
/// permission gating.
/// </summary>
public sealed class NeonRenderer : IRadialRenderer
{
    /// <summary>Stable id persisted into ProfileSettings.RadialRenderer.</summary>
    public const string RendererId = "Neon";

    private IRadialThemeTokens? _tokens;

    public string Id => RendererId;

    public void Initialize(IRadialThemeTokens tokens)
    {
        // Called at menu open / theme change. Only read tokens here — do not cache
        // theme-independent state across Initialize calls.
        _tokens = tokens;
    }

    public IRadialSlotHighlight ResolveHighlight(bool isActive)
    {
        if (!isActive || _tokens is null)
        {
            return RadialSlotHighlight.None;
        }

        return new RadialSlotHighlight
        {
            GlowBrush = _tokens.ActiveGlow,
            StrokeBrush = _tokens.AccentHover,
            StrokeThickness = 2.0,
            EffectKind = RadialSlotEffectKind.Blur,
            BlurRadius = 18.0,
            Opacity = 0.85
        };
    }

    public void RenderDecorations(Canvas canvas, double cx, double cy, double wheelRadius, double coreRadius)
    {
        // Only UI-thread code touches the canvas. Everything we add must be
        // non-hit-testable so decorations never steal hover from slots.
        var accent = _tokens?.Accent;
        if (accent is null)
        {
            return;
        }

        var dashedRing = new Ellipse
        {
            Width = wheelRadius * 2,
            Height = wheelRadius * 2,
            Stroke = accent,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            StrokeDashCap = PenLineCap.Round,
            IsHitTestVisible = false,
            Opacity = 0.6
        };
        Canvas.SetLeft(dashedRing, cx - wheelRadius);
        Canvas.SetTop(dashedRing, cy - wheelRadius);
        canvas.Children.Add(dashedRing);

        var innerRing = new Ellipse
        {
            Width = coreRadius * 2 + 14,
            Height = coreRadius * 2 + 14,
            Stroke = accent,
            StrokeThickness = 1.0,
            IsHitTestVisible = false,
            Opacity = 0.35
        };
        Canvas.SetLeft(innerRing, cx - (coreRadius + 7));
        Canvas.SetTop(innerRing, cy - (coreRadius + 7));
        canvas.Children.Add(innerRing);
    }
}

/// <summary>
/// Plugin entry point. Registers the renderer on enable and unregisters it on
/// disable; the host kernel also performs an unconditional owner cleanup, so a
/// crashed OnDisableAsync can never leave dangling contributions behind.
/// </summary>
public sealed class NeonRendererPlugin : IPulsarPlugin, IPluginLifecycle
{
    private readonly NeonRenderer _renderer = new();
    private readonly IRadialRendererRegistry _registry;
    private ILogger<NeonRendererPlugin>? _logger;

    // Constructor injection is resolved by the host's PluginFactory.
    public NeonRendererPlugin(IRadialRendererRegistry registry)
    {
        _registry = registry;
    }

    public string Id => "com.pulsar.sample.neonrenderer";
    public string DisplayName => "Neon Renderer (Sample)";
    public string Description => "Sample radial renderer: dashed neon ring + soft blur highlight";
    public string Version => "1.0.0";
    public string Author => "Pulsar Project";
    public string Icon => "💜";
    public bool CanDisable => true;

    public void Initialize(IServiceProvider serviceProvider)
    {
        _logger = serviceProvider.GetService(typeof(ILogger<NeonRendererPlugin>)) as ILogger<NeonRendererPlugin>;
    }

    public Task OnEnableAsync()
    {
        var ok = _registry.Register(_renderer, Id);
        _logger?.LogInformation("[NeonRenderer] Renderer registration {Result} (owner {OwnerId})", ok ? "succeeded" : "rejected", Id);
        return Task.CompletedTask;
    }

    public Task OnDisableAsync()
    {
        _registry.Unregister(NeonRenderer.RendererId, Id);
        _logger?.LogInformation("[NeonRenderer] Renderer unregistered");
        return Task.CompletedTask;
    }

    public Task OnUnloadAsync() => Task.CompletedTask;

    public Task<PluginResult> ExecuteAsync(
        string action,
        IReadOnlyDictionary<string, string> args,
        PulsarContext context,
        CancellationToken cancellationToken = default)
    {
        // Pure renderer plugin — no slot actions.
        return Task.FromResult(PluginResult.Ok("Neon renderer is active. Select it in Settings → General → Appearance."));
    }

    public void Dispose()
    {
    }
}
