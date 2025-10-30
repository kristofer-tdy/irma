namespace Flir.Irma.WebApi.Models;

public class HealthDependencyDto
{
    public required string Name { get; init; }
    public required string Status { get; init; }
    public int? LatencyMs { get; init; }
    public string? Message { get; init; }
}
