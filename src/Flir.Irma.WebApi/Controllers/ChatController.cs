using Flir.Irma.WebApi.Entities;
using Flir.Irma.WebApi.Extensions;
using Flir.Irma.WebApi.Infrastructure;
using Flir.Irma.WebApi.Models;
using Flir.Irma.WebApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace Flir.Irma.WebApi.Controllers;

[ApiController]
[Route("v1/irma/conversations/{conversationId:guid}")]
public class ChatController(
    IConversationService conversationService,
    ILogger<ChatController> logger)
    : ControllerBase
{
    [HttpPost("chat")]
    [ProducesResponseType(typeof(ConversationWithMessagesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Chat(Guid conversationId, [FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var conversation = await conversationService.AppendMessagesAsync(conversationId, request, cancellationToken);
        if (conversation is null)
        {
            return NotFound(new { code = "NotFound", message = $"Conversation '{conversationId}' not found.", traceId = HttpContext.TraceIdentifier });
        }

        if (conversation.State != ConversationState.Active)
        {
            return Conflict(new
            {
                code = "Conflict",
                message = $"Conversation '{conversationId}' is in state '{conversation.State}' and cannot receive new messages.",
                traceId = HttpContext.TraceIdentifier
            });
        }

        return Ok(conversation.ToConversationWithMessagesDto());
    }

    [HttpPost("chatOverStream")]
    [Produces("text/event-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChatOverStream(Guid conversationId, [FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var conversation = await conversationService.AppendMessagesAsync(conversationId, request, cancellationToken);
        if (conversation is null)
        {
            return NotFound(new { code = "NotFound", message = $"Conversation '{conversationId}' not found.", traceId = HttpContext.TraceIdentifier });
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["Transfer-Encoding"] = "chunked";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.ContentType = "text/event-stream";

        var assistantMessage = conversation.Messages
            .Where(m => m.Role == Entities.MessageRole.Assistant)
            .OrderByDescending(m => m.CreatedDateTimeUtc)
            .FirstOrDefault();

        if (conversation.State != ConversationState.Active)
        {
            await SseWriter.WriteEventAsync(Response, "error", new
            {
                code = "Conflict",
                message = $"Conversation '{conversationId}' is in state '{conversation.State}' and cannot receive new messages.",
                traceId = HttpContext.TraceIdentifier
            }, cancellationToken);
            return new EmptyResult();
        }

        if (assistantMessage is null)
        {
            logger.LogWarning("No assistant message generated for conversation {ConversationId}", conversationId);
        }
        else
        {
            await SseWriter.WriteEventAsync(Response, null, new
            {
                conversationId = conversation.ConversationId,
                messages = new[]
                {
                    new
                    {
                        messageId = assistantMessage.MessageId,
                        role = assistantMessage.Role.ToString(),
                        text = assistantMessage.Text,
                        createdDateTime = DateTime.SpecifyKind(assistantMessage.CreatedDateTimeUtc, DateTimeKind.Utc)
                    }
                }
            }, cancellationToken);
        }

        await SseWriter.WriteEventAsync(Response, "end", new
        {
            conversationId = conversation.ConversationId,
            messages = Array.Empty<object>()
        }, cancellationToken);

        return new EmptyResult();
    }
}
