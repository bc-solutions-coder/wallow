namespace Wallow.Api;

public sealed class PerformanceOptions
{
    public const string SectionName = "Performance";

    public int ThreadPoolMinWorkerThreads { get; set; }
    public int ThreadPoolMinCompletionPortThreads { get; set; }
    public long? KestrelMaxConcurrentConnections { get; set; }
    public long? KestrelMaxConcurrentUpgradedConnections { get; set; }
}
