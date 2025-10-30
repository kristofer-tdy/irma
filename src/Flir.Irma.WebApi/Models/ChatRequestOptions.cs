using System.ComponentModel.DataAnnotations;

namespace Flir.Irma.WebApi.Models;

public class ChatRequestOptions
{
    [Range(16, 8192)]
    public int? MaxTokens { get; init; }

    [Range(0, 2)]
    public float? Temperature { get; init; }
}
