using Purfle.Runtime.Adapters;
using Purfle.Runtime.Lifecycle;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Sandbox;

namespace Purfle.Runtime.Anthropic;

/// <summary>
/// Runs an <see cref="AgentRunner"/> on a recurring interval derived from the
/// agent's <c>schedule</c> block.
///
/// <para>
/// By default creates an <see cref="AnthropicAdapter"/> from the supplied manifest
/// and sandbox. Pass a custom <paramref name="adapter"/> to override (e.g. in tests).
/// </para>
/// </summary>
public sealed class Scheduler : IDisposable
{
    private const int DefaultIntervalMinutes = 15;

    private readonly AgentRunner _runner;
    private readonly TimeSpan _interval;
    private Timer? _timer;

    /// <param name="manifest">The loaded agent manifest.</param>
    /// <param name="sandbox">The agent's permission sandbox.</param>
    /// <param name="promptsDirectory">
    /// Directory containing <c>system.md</c>, or <c>null</c> to use the default prompt.
    /// </param>
    /// <param name="outputPath">Directory where <c>run.log</c> is written.</param>
    /// <param name="adapter">
    /// Optional adapter override. Defaults to a new <see cref="AnthropicAdapter"/>
    /// built from <paramref name="manifest"/> and <paramref name="sandbox"/>.
    /// </param>
    public Scheduler(
        AgentManifest manifest,
        AgentSandbox sandbox,
        string? promptsDirectory,
        string outputPath,
        ILlmAdapter? adapter = null)
    {
        var llmAdapter = adapter ?? (ILlmAdapter)new AnthropicAdapter(manifest, sandbox);
        _runner        = new AgentRunner(llmAdapter, promptsDirectory, outputPath);
        _interval      = TimeSpan.FromMinutes(
            manifest.Schedule?.IntervalMinutes ?? DefaultIntervalMinutes);
    }

    /// <summary>
    /// Starts the recurring timer. The first run fires after one full interval.
    /// </summary>
    public void Start(CancellationToken ct = default)
    {
        _timer = new Timer(
            _ => _ = _runner.RunAsync(ct),
            state:    null,
            dueTime:  _interval,
            period:   _interval);
    }

    /// <inheritdoc/>
    public void Dispose() => _timer?.Dispose();
}
