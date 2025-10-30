using System.ComponentModel.DataAnnotations;

namespace Flir.Irma.WebApi.Models;

public class ChatRequest
{
    [Required]
    [MinLength(1)]
    public required string Message { get; init; }

    [Required]
    [RegularExpression(@"^[^/]+/[^/]+$", ErrorMessage = "Product must follow ProductName/Version format.")]
    public required string Product { get; init; }

    public IReadOnlyCollection<ContextMessage> AdditionalContext { get; init; } = Array.Empty<ContextMessage>();

    public ChatRequestOptions? Options { get; init; }
}
