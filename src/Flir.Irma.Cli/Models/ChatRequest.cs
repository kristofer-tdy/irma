namespace Flir.Irma.Cli.Models;

internal sealed class ChatRequest
{
    public required string Message { get; set; }
    public required string Product { get; set; }
    public IReadOnlyList<ContextMessage> AdditionalContext { get; set; } = Array.Empty<ContextMessage>();
    public ChatRequestOptions? Options { get; set; }
}

internal sealed class ContextMessage
{
    public required string Text { get; set; }
    public string? Description { get; set; }
}

internal sealed class ChatRequestOptions
{
    public int? MaxTokens { get; set; }
    public float? Temperature { get; set; }
}
