namespace Flir.Irma.WebApi.Models;

public class VersionResponseDto
{
    public required string Version { get; init; }
    public required string Commit { get; init; }
    public required string BuildDate { get; init; }
    public required string Runtime { get; init; }
    public string? Environment { get; init; }
}
