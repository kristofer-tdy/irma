namespace Flir.Irma.WebApi.Entities;

public class ConversationEntity
{
    public Guid ConversationId { get; set; }
    public DateTime CreatedDateTimeUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Product { get; set; }
    public ConversationState State { get; set; }
    public int TurnCount { get; set; }

    public ICollection<MessageEntity> Messages { get; set; } = new List<MessageEntity>();
}
