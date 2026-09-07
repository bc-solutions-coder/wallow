namespace Wallow.ServiceDefaults;

/// <summary>
/// Records worker failure for the entry point exit code. Capture this instance before
/// RunAsync disposes the host; a background-service exception alone need not fail the process.
/// </summary>
public sealed class WorkerRunOutcome
{
    /// <summary>
    /// Gets a value indicating whether the worker's run failed.
    /// </summary>
    public bool Failed { get; private set; }

    /// <summary>
    /// Exit code 1 after MarkFailed, otherwise 0.
    /// </summary>
    public int ExitCode => Failed ? 1 : 0;

    /// <summary>
    /// Marks the run as failed. Called from the worker's exception path.
    /// </summary>
    public void MarkFailed() => Failed = true;
}
