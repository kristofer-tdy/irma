using Flir.Irma.WebApi.Extensions;
using Flir.Irma.WebApi.Models;
using Flir.Irma.WebApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace Flir.Irma.WebApi.Controllers;

[ApiController]
[Route("v1/irma/conversations")]
public class ConversationsController(IConversationService conversationService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest request, CancellationToken cancellationToken)
    {
        var conversation = await conversationService.CreateConversationAsync(request, cancellationToken);
        var dto = conversation.ToDto();

        return CreatedAtRoute(nameof(GetConversation), new { conversationId = dto.ConversationId }, dto);
    }

    [HttpGet("{conversationId:guid}", Name = nameof(GetConversation))]
    [ProducesResponseType(typeof(ConversationWithMessagesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConversation(Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await conversationService.GetConversationAsync(conversationId, cancellationToken, includeMessages: true);
        if (conversation is null)
        {
            return NotFound(new { code = "NotFound", message = $"Conversation '{conversationId}' not found.", traceId = HttpContext.TraceIdentifier });
        }

        return Ok(conversation.ToConversationWithMessagesDto());
    }
}
