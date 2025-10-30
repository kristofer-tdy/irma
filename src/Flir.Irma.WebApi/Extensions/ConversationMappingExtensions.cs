using Flir.Irma.WebApi.Entities;
using Flir.Irma.WebApi.Models;

namespace Flir.Irma.WebApi.Extensions;

public static class ConversationMappingExtensions
{
    public static ConversationDto ToDto(this ConversationEntity entity) =>
        new()
        {
            ConversationId = entity.ConversationId,
            CreatedDateTime = DateTime.SpecifyKind(entity.CreatedDateTimeUtc, DateTimeKind.Utc),
            DisplayName = entity.DisplayName,
            State = entity.State.ToString(),
            TurnCount = entity.TurnCount,
            Product = entity.Product
        };

    public static ConversationWithMessagesDto ToConversationWithMessagesDto(this ConversationEntity entity)
    {
        var baseDto = entity.ToDto();
        return new ConversationWithMessagesDto
        {
            ConversationId = baseDto.ConversationId,
            CreatedDateTime = baseDto.CreatedDateTime,
            DisplayName = baseDto.DisplayName,
            State = baseDto.State,
            TurnCount = baseDto.TurnCount,
            Product = baseDto.Product,
            Messages = entity.Messages
                .OrderBy(m => m.CreatedDateTimeUtc)
                .Select(m => new MessageDto
                {
                    MessageId = m.MessageId,
                    Role = m.Role.ToString(),
                    Text = m.Text,
                    CreatedDateTime = DateTime.SpecifyKind(m.CreatedDateTimeUtc, DateTimeKind.Utc)
                })
                .ToArray()
        };
    }
}
