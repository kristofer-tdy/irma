using System.ComponentModel.DataAnnotations;

namespace Flir.Irma.WebApi.Models;

public class ContextMessage
{
    [Required]
    public required string Text { get; init; }

    public string? Description { get; init; }
}
