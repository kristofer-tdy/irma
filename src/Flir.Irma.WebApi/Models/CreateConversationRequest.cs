using System.ComponentModel.DataAnnotations;

namespace Flir.Irma.WebApi.Models;

public class CreateConversationRequest
{
    [RegularExpression(@"^[^/]+/[^/]+$", ErrorMessage = "Product must follow ProductName/Version format.")]
    public string? Product { get; init; }

    public IReadOnlyCollection<ContextMessage> AdditionalContext { get; init; } = Array.Empty<ContextMessage>();
}
