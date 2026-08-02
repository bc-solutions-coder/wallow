namespace Wallow.ServiceDefaults;

/// <summary>
/// Records whether a one-shot worker's run failed, so <c>Program.cs</c> can turn that into a
/// non-zero process exit code.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="Microsoft.Extensions.Hosting.BackgroundService"/> that throws does NOT fail the
/// process. The host catches the faulted <c>ExecuteAsync</c> task, logs it, stops the application,
/// and never rethrows — so <c>RunAsync()</c> completes successfully and the process exits 0. That
/// is true regardless of <c>BackgroundServiceExceptionBehavior</c>, which governs whether the host
/// stops, not the exit code. Without this sink a failed seed or migration reports success, and a
/// Compose <c>depends_on: condition: service_completed_successfully</c> edge lets dependents start
/// against a half-built database.
/// </para>
/// <para>
/// Resolve this from <c>host.Services</c> BEFORE awaiting <c>host.RunAsync()</c>. <c>RunAsync</c>
/// disposes the host in a <c>finally</c>, so resolving afterwards throws
/// <see cref="ObjectDisposedException"/>.
/// </para>
/// </remarks>
public sealed class WorkerRunOutcome
{
    /// <summary>
    /// Gets a value indicating whether the worker's run failed.
    /// </summary>
    public bool Failed { get; private set; }

    /// <summary>
    /// Gets the process exit code this run should produce.
    /// </summary>
    public int ExitCode => Failed ? 1 : 0;

    /// <summary>
    /// Marks the run as failed. Called from the worker's exception path.
    /// </summary>
    public void MarkFailed() => Failed = true;
}
