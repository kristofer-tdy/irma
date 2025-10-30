namespace Flir.Irma.Cli.Models;

internal sealed class VersionResponse
{
    public string Version { get; set; } = string.Empty;
    public string Commit { get; set; } = string.Empty;
    public string BuildDate { get; set; } = string.Empty;
    public string Runtime { get; set; } = string.Empty;
    public string? Environment { get; set; }
}
