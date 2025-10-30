namespace Flir.Irma.WebApi.Models;

public class HealthResponseDto
{
    public required string Status { get; init; }
    public required long UptimeSeconds { get; init; }
    public required IReadOnlyCollection<HealthDependencyDto> Dependencies { get; init; }
    public required string TraceId { get; init; }
}
