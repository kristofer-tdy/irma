namespace Flir.Irma.Cli.Models;

internal sealed class ConversationDetail : ConversationSummary
{
    public IReadOnlyList<MessageSummary> Messages { get; set; } = Array.Empty<MessageSummary>();
}
