namespace Flir.Irma.WebApi.Models;

public class ConversationWithMessagesDto : ConversationDto
{
    public required IReadOnlyCollection<MessageDto> Messages { get; init; }
}
