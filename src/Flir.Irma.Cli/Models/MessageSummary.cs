namespace Flir.Irma.Cli.Models;

internal sealed class MessageSummary
{
    public Guid MessageId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedDateTime { get; set; }
}
