using Flir.Irma.WebApi.Data;
using Flir.Irma.WebApi.Entities;
using Flir.Irma.WebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace Flir.Irma.WebApi.Services;

public class ConversationService(
    IrmaDbContext dbContext,
    ILogger<ConversationService> logger)
    : IConversationService
{
    public async Task<ConversationEntity> CreateConversationAsync(CreateConversationRequest request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var entity = new ConversationEntity
        {
            ConversationId = Guid.NewGuid(),
            CreatedDateTimeUtc = now,
            LastModifiedUtc = now,
            DisplayName = string.Empty,
            Product = request.Product,
            State = ConversationState.Active,
            TurnCount = 0
        };

        await dbContext.Conversations.AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Conversation {ConversationId} created for product {Product}", entity.ConversationId, entity.Product ?? "n/a");

        return entity;
    }

    public async Task<ConversationEntity?> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken, bool includeMessages = false)
    {
        IQueryable<ConversationEntity> query = dbContext.Conversations;

        if (includeMessages)
        {
            query = query.Include(c => c.Messages);
        }

        return await query.FirstOrDefaultAsync(c => c.ConversationId == conversationId, cancellationToken);
    }

    public async Task<ConversationEntity?> AppendMessagesAsync(Guid conversationId, ChatRequest request, CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.ConversationId == conversationId, cancellationToken);

        if (conversation is null)
        {
            return null;
        }

        if (conversation.State != ConversationState.Active)
        {
            logger.LogWarning("Conversation {ConversationId} is in state {State} and cannot accept new messages", conversationId, conversation.State);
            return conversation;
        }

        var now = DateTime.UtcNow;
        var userMessage = new MessageEntity
        {
            MessageId = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = MessageRole.User,
            Text = request.Message,
            CreatedDateTimeUtc = now
        };

        var assistantMessage = new MessageEntity
        {
            MessageId = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = MessageRole.Assistant,
            Text = BuildAssistantPlaceholderResponse(request),
            CreatedDateTimeUtc = now.AddMilliseconds(50)
        };

        conversation.Messages.Add(userMessage);
        conversation.Messages.Add(assistantMessage);

        conversation.TurnCount += 1;
        conversation.LastModifiedUtc = assistantMessage.CreatedDateTimeUtc;

        if (string.IsNullOrWhiteSpace(conversation.DisplayName))
        {
            conversation.DisplayName = request.Message.Length > 80
                ? request.Message[..80]
                : request.Message;
        }

        if (!string.IsNullOrWhiteSpace(request.Product))
        {
            conversation.Product = request.Product;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Appended messages to conversation {ConversationId}", conversationId);

        return conversation;
    }

    private static string BuildAssistantPlaceholderResponse(ChatRequest request)
    {
        // TODO: Replace placeholder response with Azure AI Foundry integration.
        return $"TODO: Integrate with Azure AI Foundry. Echo: \"{request.Message}\"";
    }
}
