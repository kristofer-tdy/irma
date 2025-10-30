namespace Flir.Irma.WebApi.Models;

public class MessageDto
{
    public required Guid MessageId { get; init; }
    public required string Role { get; init; }
    public required string Text { get; init; }
    public required DateTime CreatedDateTime { get; init; }
}
