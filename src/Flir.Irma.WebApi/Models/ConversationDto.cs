namespace Flir.Irma.WebApi.Models;

public class ConversationDto
{
    public required Guid ConversationId { get; init; }
    public required DateTime CreatedDateTime { get; init; }
    public required string DisplayName { get; init; }
    public required string State { get; init; }
    public required int TurnCount { get; init; }
    public string? Product { get; init; }
}
