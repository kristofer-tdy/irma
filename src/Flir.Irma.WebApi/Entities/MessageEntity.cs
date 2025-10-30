namespace Flir.Irma.WebApi.Entities;

public class MessageEntity
{
    public Guid MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public MessageRole Role { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedDateTimeUtc { get; set; }

    public ConversationEntity? Conversation { get; set; }
}
