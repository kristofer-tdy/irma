using Flir.Irma.WebApi.Entities;
using Flir.Irma.WebApi.Models;

namespace Flir.Irma.WebApi.Services;

public interface IConversationService
{
    Task<ConversationEntity> CreateConversationAsync(CreateConversationRequest request, CancellationToken cancellationToken);
    Task<ConversationEntity?> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken, bool includeMessages = false);
    Task<ConversationEntity?> AppendMessagesAsync(Guid conversationId, ChatRequest request, CancellationToken cancellationToken);
}
