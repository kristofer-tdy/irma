using System.Text.Json;

namespace Flir.Irma.WebApi.Infrastructure;

public static class SseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static async Task WriteEventAsync(HttpResponse response, string? eventName, object payload, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(eventName))
        {
            await response.WriteAsync($"event: {eventName}\n", cancellationToken);
        }

        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        await response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
