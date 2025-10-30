using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Flir.Irma.Cli.Configuration;
using Flir.Irma.Cli.Models;

namespace Flir.Irma.Cli.Services;

internal sealed class IrmaApiClient : IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly bool _disposeClient;

    public IrmaApiClient(Uri baseUri, AuthTicket? ticket = null, HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _disposeClient = httpClient is null;

        _httpClient.BaseAddress = baseUri;
        _httpClient.Timeout = TimeSpan.FromSeconds(100);

        if (ticket is not null && !ticket.IsExpired())
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(ticket.Scheme, ticket.AccessToken);
        }
    }

    public async Task<ApiResponse<HealthResponse>> GetHealthAsync(CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/irma/healthz");
        return await SendAsync<HealthResponse>(request, cancellationToken);
    }

    public async Task<ApiResponse<VersionResponse>> GetVersionAsync(CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/irma/version");
        return await SendAsync<VersionResponse>(request, cancellationToken);
    }

    public async Task<ApiResponse<ConversationSummary>> CreateConversationAsync(string? product, IReadOnlyCollection<ContextMessage>? additionalContext, CancellationToken cancellationToken)
    {
        var payload = new
        {
            product,
            additionalContext = additionalContext ?? Array.Empty<ContextMessage>()
        };

        var body = JsonContent(payload);
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/irma/conversations")
        {
            Content = body
        };

        return await SendAsync<ConversationSummary>(request, cancellationToken);
    }

    public async Task<ApiResponse<ConversationDetail>> ChatAsync(Guid conversationId, ChatRequest requestPayload, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/irma/conversations/{conversationId}/chat")
        {
            Content = JsonContent(requestPayload)
        };

        return await SendAsync<ConversationDetail>(request, cancellationToken);
    }

    public async Task<ApiResponse<ConversationDetail>> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/irma/conversations/{conversationId}");
        return await SendAsync<ConversationDetail>(request, cancellationToken);
    }

    public async IAsyncEnumerable<ApiStreamEvent> ChatOverStreamAsync(Guid conversationId, ChatRequest requestPayload, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/irma/conversations/{conversationId}/chatOverStream")
        {
            Content = JsonContent(requestPayload)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var raw = await response.Content.ReadAsStreamAsync(cancellationToken);

        using var reader = new StreamReader(raw, Encoding.UTF8);
        string? line;
        var currentEvent = new StringBuilder();
        string? eventName = null;

        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            if (string.IsNullOrEmpty(line))
            {
                if (currentEvent.Length > 0)
                {
                    var payload = currentEvent.ToString().TrimEnd();
                    yield return new ApiStreamEvent(eventName, payload);
                    currentEvent.Clear();
                    eventName = null;
                }
                continue;
            }

            if (line.StartsWith("event:"))
            {
                eventName = line[6..].Trim();
                continue;
            }

            if (line.StartsWith("data:"))
            {
                if (currentEvent.Length > 0)
                {
                    currentEvent.AppendLine();
                }

                currentEvent.Append(line[5..].Trim());
            }
        }
    }

    private static StringContent JsonContent(object value)
    {
        var json = JsonSerializer.Serialize(value, SerializerOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private async Task<ApiResponse<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new ApiResponse<T>(response.StatusCode, default, rawBody, false);
        }

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return new ApiResponse<T>(response.StatusCode, default, rawBody, true);
        }

        var body = JsonSerializer.Deserialize<T>(rawBody, SerializerOptions);
        return new ApiResponse<T>(response.StatusCode, body, rawBody, true);
    }

    public void Dispose()
    {
        if (_disposeClient)
        {
            _httpClient.Dispose();
        }
    }
}

internal sealed record ApiResponse<T>(HttpStatusCode StatusCode, T? Body, string RawBody, bool IsSuccess);

internal sealed record ApiStreamEvent(string? EventName, string RawData)
{
    public bool IsError => string.Equals(EventName, "error", StringComparison.OrdinalIgnoreCase);
    public bool IsEnd => string.Equals(EventName, "end", StringComparison.OrdinalIgnoreCase);
}
