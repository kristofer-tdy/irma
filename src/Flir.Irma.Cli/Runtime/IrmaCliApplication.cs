using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Text.Json;
using Flir.Irma.Cli.Configuration;
using Flir.Irma.Cli.Models;
using Flir.Irma.Cli.Services;

namespace Flir.Irma.Cli.Runtime;

internal sealed class IrmaCliApplication
{
    private readonly DefaultSettingsStore _defaultsStore = new();
    private readonly AuthStore _authStore = new();
    private readonly SessionStore _sessionStore = new();

    private readonly Option<string?> _baseUrlOption = new("--base-url");
    private readonly Option<bool> _jsonOption = new("--json");
    private readonly Option<string?> _productOption = new("--product");

    private readonly Option<bool> _noStreamOption = new("--no-stream");
    private readonly Option<Guid?> _conversationIdOption = new("--conversation-id");
    private readonly Option<FileInfo?> _contextFileOption = new("--context");

    private readonly Option<bool> _deviceCodeOption = new("--device-code");
    private readonly Option<bool> _clientCredentialsOption = new("--client-credentials");

    public IrmaCliApplication()
    {
        _baseUrlOption.Description = "The base URL of the Irma Web API (e.g. https://localhost:5001)";
        _jsonOption.Description = "Output raw JSON responses where applicable.";
        _productOption.Description = "Override the product identifier for this command (format Product/Version).";

        _noStreamOption.Description = "Use the synchronous chat endpoint instead of streaming.";
        _conversationIdOption.Description = "Target conversation identifier.";
        _contextFileOption.Description = "Path to a JSON file with additional context payload.";

        _deviceCodeOption.Description = "Use the device code flow (stubbed).";
        _clientCredentialsOption.Description = "Use the client credentials flow (stubbed).";

        _deviceCodeOption.DefaultValueFactory = _ => true;
    }

    public async Task<int> RunAsync(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        var root = BuildRootCommand();
        var parserConfiguration = new ParserConfiguration
        {
            EnablePosixBundling = false
        };

        try
        {
            var parseResult = root.Parse(args, parserConfiguration);
            var invocationConfiguration = new InvocationConfiguration
            {
                EnableDefaultExceptionHandler = false
            };

            return await parseResult.InvokeAsync(invocationConfiguration, cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation cancelled.");
            return 1;
        }
    }

    private RootCommand BuildRootCommand()
    {
        var root = new RootCommand("Irma command-line client");
        root.Add(_baseUrlOption);
        root.Add(_jsonOption);
        root.Add(_productOption);

        root.Add(BuildLoginCommand());
        root.Add(BuildLogoutCommand());
        root.Add(BuildDefaultsCommand());
        root.Add(BuildHealthCommand());
        root.Add(BuildNewConversationCommand());
        root.Add(BuildAskCommand());
        root.Add(BuildChatCommand());

        return root;
    }

    private Command BuildLoginCommand()
    {
        var command = new Command("login", "Authenticate with Irma using Azure Entra ID B2C (stub implementation).");
        command.Add(_deviceCodeOption);
        command.Add(_clientCredentialsOption);

        command.SetAction((parseResult, cancellationToken) =>
            ExecuteAsync(parseResult, async (result, state, ct) =>
            {
                var useClientCredentials = result.GetValue(_clientCredentialsOption);
                var flow = useClientCredentials ? "client credentials" : "device code";
                Console.WriteLine($"Starting stubbed {flow} login flow... (TODO: integrate Azure Entra ID B2C)");

                var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                var ticket = new AuthTicket(token, DateTimeOffset.UtcNow.AddHours(1));
                state.SetAuth(ticket);

                Console.WriteLine("Authentication stub complete. Token cached locally.");
                return 0;
            }, cancellationToken));

        return command;
    }

    private Command BuildLogoutCommand()
    {
        var command = new Command("logout", "Clear cached authentication tokens.");
        command.SetAction((parseResult, cancellationToken) =>
            ExecuteAsync(parseResult, (result, state, ct) =>
            {
                if (state.AuthTicket is null)
                {
                    Console.WriteLine("You are already logged out.");
                    return Task.FromResult(0);
                }

                state.ClearAuth();
                Console.WriteLine("Cached tokens removed.");
                return Task.FromResult(0);
            }, cancellationToken));

        return command;
    }

    private Command BuildDefaultsCommand()
    {
        var defaultsCommand = new Command("defaults", "Manage local default settings.");

        var setCommand = new Command("set", "Persist a default setting.");
        var keyArgument = new Argument<string>("key") { Description = "Setting key (e.g. product, base-url)." };
        var valueArgument = new Argument<string>("value") { Description = "Value to store." };
        setCommand.Add(keyArgument);
        setCommand.Add(valueArgument);
        setCommand.SetAction((parseResult, cancellationToken) =>
            ExecuteAsync(parseResult, (result, state, ct) =>
            {
                var key = result.GetValue(keyArgument);
                var value = result.GetValue(valueArgument);

                if (string.Equals(key, "base-url", StringComparison.OrdinalIgnoreCase) &&
                    !Uri.TryCreate(value, UriKind.Absolute, out _))
                {
                    Console.Error.WriteLine("Invalid base-url. Provide an absolute URI (e.g. https://localhost:5001).");
                    return Task.FromResult(1);
                }

                state.SetDefault(key, value);
                Console.WriteLine($"Stored default '{key}'.");
                return Task.FromResult(0);
            }, cancellationToken));

        var listCommand = new Command("list", "List stored defaults.");
        listCommand.SetAction((parseResult, cancellationToken) =>
            ExecuteAsync(parseResult, (result, state, ct) =>
            {
                if (state.Defaults.Count == 0)
                {
                    Console.WriteLine("No defaults stored.");
                }
                else
                {
                    foreach (var pair in state.Defaults.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"{pair.Key}: {pair.Value}");
                    }
                }

                return Task.FromResult(0);
            }, cancellationToken));

        var clearCommand = new Command("clear", "Remove a stored default.");
        var clearKeyArgument = new Argument<string>("key") { Description = "Setting key to remove." };
        clearCommand.Add(clearKeyArgument);
        clearCommand.SetAction((parseResult, cancellationToken) =>
            ExecuteAsync(parseResult, (result, state, ct) =>
            {
                var key = result.GetValue(clearKeyArgument);
                var removed = state.ClearDefault(key);
                if (removed)
                {
                    Console.WriteLine($"Removed default '{key}'.");
                    return Task.FromResult(0);
                }

                Console.WriteLine($"Default '{key}' was not set.");
                return Task.FromResult(1);
            }, cancellationToken));

        defaultsCommand.Add(setCommand);
        defaultsCommand.Add(listCommand);
        defaultsCommand.Add(clearCommand);

        return defaultsCommand;
    }

    private Command BuildHealthCommand()
    {
        var command = new Command("health", "Call the Irma health endpoint.");

        command.SetAction((parseResult, cancellationToken) =>
            ExecuteWithClientAsync(parseResult, async (result, state, client, json, ct) =>
            {
                var response = await client.GetHealthAsync(ct);
                if (!response.IsSuccess)
                {
                    return HandleError(response.StatusCode, response.RawBody);
                }

                if (json)
                {
                    Console.WriteLine(response.RawBody);
                }
                else if (response.Body is HealthResponse health)
                {
                    Console.WriteLine($"Status: {health.Status}");
                    Console.WriteLine($"Uptime: {TimeSpan.FromSeconds(health.UptimeSeconds)}");
                    foreach (var dependency in health.Dependencies)
                    {
                        var line = $" - {dependency.Name}: {dependency.Status}";
                        if (!string.IsNullOrWhiteSpace(dependency.Message))
                        {
                            line += $" ({dependency.Message})";
                        }

                        Console.WriteLine(line);
                    }
                    Console.WriteLine($"TraceId: {health.TraceId}");
                }

                return 0;
            }, cancellationToken));

        return command;
    }

    private Command BuildNewConversationCommand()
    {
        var command = new Command("new-conversation", "Start a new conversation and cache the identifier.");

        command.SetAction((parseResult, cancellationToken) =>
            ExecuteWithClientAsync(parseResult, async (result, state, client, json, ct) =>
            {
                var product = ResolveProduct(result, state, required: false);
                var response = await client.CreateConversationAsync(product, null, ct);
                if (!response.IsSuccess || response.Body is null)
                {
                    return HandleError(response.StatusCode, response.RawBody);
                }

                state.SetCurrentConversation(response.Body.ConversationId);

                if (json)
                {
                    Console.WriteLine(response.RawBody);
                }
                else
                {
                    Console.WriteLine($"Conversation created: {response.Body.ConversationId}");
                }

                return 0;
            }, cancellationToken));

        return command;
    }

    private Command BuildAskCommand()
    {
        var messageArgument = new Argument<string>("message") { Description = "The prompt to send to Irma." };
        var command = new Command("ask", "Send a single question to Irma.");
        command.Add(messageArgument);
        command.Add(_noStreamOption);
        command.Add(_conversationIdOption);
        command.Add(_contextFileOption);

        command.SetAction((parseResult, cancellationToken) =>
            ExecuteWithClientAsync(parseResult, async (result, state, client, json, ct) =>
            {
                var message = result.GetValue(messageArgument);
                var useNoStream = result.GetValue(_noStreamOption);
                var conversationId = result.GetValue(_conversationIdOption) ?? state.Session.CurrentConversationId;

                if (conversationId is null)
                {
                    Console.Error.WriteLine("No conversation selected. Run 'irma new-conversation' first or provide --conversation-id.");
                    return 1;
                }

                var product = ResolveProduct(result, state, required: true);
                if (product is null)
                {
                    Console.Error.WriteLine("Product is required. Use --product or 'irma defaults set product <value>'.");
                    return 1;
                }

                var additionalContext = await LoadAdditionalContextAsync(result.GetValue(_contextFileOption), ct);
                var chatRequest = new ChatRequest
                {
                    Message = message,
                    Product = product,
                    AdditionalContext = additionalContext
                };

                if (useNoStream)
                {
                    var response = await client.ChatAsync(conversationId.Value, chatRequest, ct);
                    if (!response.IsSuccess || response.Body is null)
                    {
                        return HandleError(response.StatusCode, response.RawBody);
                    }

                    if (json)
                    {
                        Console.WriteLine(response.RawBody);
                    }
                    else
                    {
                        PrintMessages(response.Body.Messages);
                    }
                }
                else
                {
                    var rawEvents = new List<string>();
                    await foreach (var evt in client.ChatOverStreamAsync(conversationId.Value, chatRequest, ct))
                    {
                        if (json)
                        {
                            rawEvents.Add(evt.RawData);
                        }
                        else
                        {
                            RenderStreamEvent(evt);
                        }

                        if (evt.IsEnd)
                        {
                            break;
                        }
                    }

                    if (json && rawEvents.Count > 0)
                    {
                        Console.WriteLine("[" + string.Join(',', rawEvents) + "]");
                    }
                }

                state.SetCurrentConversation(conversationId.Value);
                return 0;
            }, cancellationToken));

        return command;
    }

    private Command BuildChatCommand()
    {
        var command = new Command("chat", "Start an interactive chat session.");

        command.SetAction((parseResult, cancellationToken) =>
            ExecuteWithClientAsync(parseResult, async (result, state, client, json, ct) =>
            {
                var conversationId = state.Session.CurrentConversationId;
                if (conversationId is null)
                {
                    var product = ResolveProduct(result, state, required: false);
                    var conversationResponse = await client.CreateConversationAsync(product, null, ct);
                    if (!conversationResponse.IsSuccess || conversationResponse.Body is null)
                    {
                        return HandleError(conversationResponse.StatusCode, conversationResponse.RawBody);
                    }

                    conversationId = conversationResponse.Body.ConversationId;
                    state.SetCurrentConversation(conversationId.Value);
                    Console.WriteLine($"Started conversation {conversationId}");
                }
                else
                {
                    Console.WriteLine($"Continuing conversation {conversationId}");
                }

                Console.WriteLine("Enter your prompt, /newConversation to reset, or press Ctrl+C to exit.");

                while (true)
                {
                    Console.Write("> ");
                    var input = Console.ReadLine();
                    if (input is null)
                    {
                        break;
                    }

                    if (string.Equals(input, "/newConversation", StringComparison.OrdinalIgnoreCase))
                    {
                        var product = ResolveProduct(result, state, required: false);
                        var newResponse = await client.CreateConversationAsync(product, null, ct);
                        if (!newResponse.IsSuccess || newResponse.Body is null)
                        {
                            Console.Error.WriteLine("Failed to create new conversation: " + newResponse.RawBody);
                            continue;
                        }

                        conversationId = newResponse.Body.ConversationId;
                        state.SetCurrentConversation(conversationId.Value);
                        Console.WriteLine($"New conversation {conversationId} started.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(input))
                    {
                        continue;
                    }

                    var productForTurn = ResolveProduct(result, state, required: true);
                    if (productForTurn is null)
                    {
                        Console.Error.WriteLine("Product is required. Use --product or defaults set product <value>.");
                        continue;
                    }

                    var request = new ChatRequest
                    {
                        Message = input,
                        Product = productForTurn,
                        AdditionalContext = Array.Empty<ContextMessage>()
                    };

                    var rawEvents = new List<string>();
                    await foreach (var evt in client.ChatOverStreamAsync(conversationId!.Value, request, ct))
                    {
                        if (json)
                        {
                            rawEvents.Add(evt.RawData);
                        }
                        else
                        {
                            RenderStreamEvent(evt);
                        }

                        if (evt.IsEnd)
                        {
                            break;
                        }
                    }

                    if (json && rawEvents.Count > 0)
                    {
                        Console.WriteLine("[" + string.Join(',', rawEvents) + "]");
                    }

                    state.SetCurrentConversation(conversationId.Value);
                }

                return 0;
            }, cancellationToken));

        return command;
    }

    private async Task<int> ExecuteAsync(ParseResult parseResult, Func<ParseResult, CliState, CancellationToken, Task<int>> handler, CancellationToken cancellationToken)
    {
        try
        {
            var state = await LoadStateAsync(cancellationToken);
            var exitCode = await handler(parseResult, state, cancellationToken);
            if (exitCode == 0)
            {
                await SaveStateAsync(state, cancellationToken);
            }

            return exitCode;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation cancelled.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            return 1;
        }
    }

    private Task<int> ExecuteWithClientAsync(ParseResult parseResult, Func<ParseResult, CliState, IrmaApiClient, bool, CancellationToken, Task<int>> handler, CancellationToken cancellationToken)
    {
        return ExecuteAsync(parseResult, async (result, state, ct) =>
        {
            var baseUri = ResolveBaseUri(result, state);
            using var client = new IrmaApiClient(baseUri, state.AuthTicket);
            var json = result.GetValue(_jsonOption);
            return await handler(result, state, client, json, ct);
        }, cancellationToken);
    }

    private async Task<CliState> LoadStateAsync(CancellationToken cancellationToken)
    {
        var defaults = await _defaultsStore.LoadAsync(cancellationToken);
        var auth = await _authStore.LoadAsync(cancellationToken);
        var session = await _sessionStore.LoadAsync(cancellationToken);
        return new CliState(defaults, auth, session);
    }

    private async Task SaveStateAsync(CliState state, CancellationToken cancellationToken)
    {
        if (state.DefaultsModified)
        {
            await _defaultsStore.SaveAsync(state.Defaults, cancellationToken);
        }

        if (state.AuthCleared)
        {
            await _authStore.ClearAsync();
        }
        else if (state.AuthModified && state.AuthTicket is not null)
        {
            await _authStore.SaveAsync(state.AuthTicket, cancellationToken);
        }

        if (state.SessionModified)
        {
            await _sessionStore.SaveAsync(state.Session, cancellationToken);
        }
    }

    private Uri ResolveBaseUri(ParseResult parseResult, CliState state)
    {
        var explicitOption = parseResult.GetValue(_baseUrlOption);
        if (!string.IsNullOrWhiteSpace(explicitOption) && Uri.TryCreate(explicitOption, UriKind.Absolute, out var explicitUri))
        {
            return NormalizeBaseUri(explicitUri);
        }

        var envValue = Environment.GetEnvironmentVariable("IRMA_BASE_URL");
        if (!string.IsNullOrWhiteSpace(envValue) && Uri.TryCreate(envValue, UriKind.Absolute, out var envUri))
        {
            return NormalizeBaseUri(envUri);
        }

        if (state.Defaults.TryGetValue("base-url", out var stored) && Uri.TryCreate(stored, UriKind.Absolute, out var storedUri))
        {
            return NormalizeBaseUri(storedUri);
        }

        return new Uri("https://localhost:5001", UriKind.Absolute);
    }

    private static Uri NormalizeBaseUri(Uri uri)
    {
        if (!string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/")
        {
            var builder = new UriBuilder(uri)
            {
                Path = "/"
            };
            return builder.Uri;
        }

        return uri;
    }

    private string? ResolveProduct(ParseResult parseResult, CliState state, bool required)
    {
        var explicitProduct = parseResult.GetValue(_productOption);
        if (!string.IsNullOrWhiteSpace(explicitProduct))
        {
            return explicitProduct;
        }

        if (state.Defaults.TryGetValue("product", out var stored) && !string.IsNullOrWhiteSpace(stored))
        {
            return stored;
        }

        return required ? null : null;
    }

    private static async Task<IReadOnlyList<ContextMessage>> LoadAdditionalContextAsync(FileInfo? fileInfo, CancellationToken cancellationToken)
    {
        if (fileInfo is null)
        {
            return Array.Empty<ContextMessage>();
        }

        if (!fileInfo.Exists)
        {
            Console.Error.WriteLine($"Context file '{fileInfo.FullName}' not found.");
            return Array.Empty<ContextMessage>();
        }

        await using var stream = fileInfo.OpenRead();
        var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            Console.Error.WriteLine("Context file must contain a JSON array of objects with 'text' properties.");
            return Array.Empty<ContextMessage>();
        }

        var messages = new List<ContextMessage>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
            {
                var message = new ContextMessage
                {
                    Text = textProp.GetString() ?? string.Empty,
                    Description = element.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String
                        ? descProp.GetString()
                        : null
                };
                messages.Add(message);
            }
        }

        return messages;
    }

    private static void PrintMessages(IEnumerable<MessageSummary> messages)
    {
        foreach (var message in messages)
        {
            var prefix = string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase) ? "Irma" : "You";
            Console.WriteLine($"{prefix}: {message.Text}");
        }
    }

    private static void RenderStreamEvent(ApiStreamEvent streamEvent)
    {
        if (streamEvent.IsError)
        {
            Console.Error.WriteLine(streamEvent.RawData);
            return;
        }

        if (streamEvent.IsEnd)
        {
            Console.WriteLine();
            return;
        }

        try
        {
            using var json = JsonDocument.Parse(streamEvent.RawData);
            if (json.RootElement.TryGetProperty("messages", out var messagesElement) && messagesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in messagesElement.EnumerateArray())
                {
                    if (item.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    {
                        Console.Write(text.GetString());
                    }
                }
            }
        }
        catch (JsonException)
        {
            Console.WriteLine(streamEvent.RawData);
        }
    }

    private static int HandleError(System.Net.HttpStatusCode statusCode, string rawBody)
    {
        Console.Error.WriteLine($"Request failed with status {(int)statusCode} ({statusCode}).");
        if (!string.IsNullOrWhiteSpace(rawBody))
        {
            Console.Error.WriteLine(rawBody);
        }

        return statusCode switch
        {
            System.Net.HttpStatusCode.BadRequest => 3,
            System.Net.HttpStatusCode.Unauthorized => 4,
            System.Net.HttpStatusCode.Forbidden => 5,
            System.Net.HttpStatusCode.NotFound => 6,
            System.Net.HttpStatusCode.Conflict => 7,
            _ => 1
        };
    }
}
