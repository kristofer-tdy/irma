using System.Net;
using System.Net.Http.Json;
using Flir.Irma.WebApi.Tests.Support;

namespace Flir.Irma.WebApi.Tests;

public class ConversationEndpointsTests : IClassFixture<SqliteWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ConversationEndpointsTests(SqliteWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateConversation_PersistsConversation()
    {
        var response = await _client.PostAsJsonAsync("/v1/irma/conversations", new { product = "Ixx/1.0" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var summary = await response.Content.ReadFromJsonAsync<TestConversationSummary>();
        Assert.NotNull(summary);
        Assert.NotEqual(Guid.Empty, summary!.ConversationId);

        var getResponse = await _client.GetAsync($"/v1/irma/conversations/{summary.ConversationId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task Chat_AddsAssistantMessage()
    {
        var createResponse = await _client.PostAsJsonAsync("/v1/irma/conversations", new { product = "Ixx/1.0" });
        var summary = await createResponse.Content.ReadFromJsonAsync<ConversationSummary>();
        Assert.NotNull(summary);

        var chatBody = new
        {
            message = "Hello Irma",
            product = "Ixx/1.0"
        };

        var chatResponse = await _client.PostAsJsonAsync($"/v1/irma/conversations/{summary!.ConversationId}/chat", chatBody);
        Assert.Equal(HttpStatusCode.OK, chatResponse.StatusCode);

        var conversation = await chatResponse.Content.ReadFromJsonAsync<TestConversationDetail>();
        Assert.NotNull(conversation);
        Assert.True(conversation!.Messages.Count >= 2);
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/v1/irma/healthz");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable);
    }
}

internal sealed record TestConversationSummary(Guid ConversationId, DateTime CreatedDateTime, string DisplayName, string State, int TurnCount, string? Product);

internal sealed record TestMessage(Guid MessageId, string Role, string Text, DateTime CreatedDateTime);

internal sealed record TestConversationDetail(Guid ConversationId, DateTime CreatedDateTime, string DisplayName, string State, int TurnCount, string? Product, IReadOnlyList<TestMessage> Messages);
