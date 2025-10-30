namespace Flir.Irma.Cli.Models;

internal class ConversationSummary
{
    public Guid ConversationId { get; set; }
    public DateTime CreatedDateTime { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int TurnCount { get; set; }
    public string? Product { get; set; }
}
