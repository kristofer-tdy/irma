namespace Flir.Irma.Cli.Models;

internal sealed class HealthResponse
{
    public string Status { get; set; } = string.Empty;
    public long UptimeSeconds { get; set; }
    public IReadOnlyList<HealthDependency> Dependencies { get; set; } = Array.Empty<HealthDependency>();
    public string TraceId { get; set; } = string.Empty;
}

internal sealed class HealthDependency
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? LatencyMs { get; set; }
    public string? Message { get; set; }
}
