using System.Text.Json;

namespace Flir.Irma.Cli.Configuration;

internal sealed class SessionStore
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _filePath;

    public SessionStore(string? filePath = null)
    {
        _filePath = filePath ?? IrmaCliPaths.SessionsFilePath;
    }

    public async Task<SessionState> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new SessionState();
        }

        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<SessionState>(stream, _jsonOptions, cancellationToken)
               ?? new SessionState();
    }

    public async Task SaveAsync(SessionState state, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, state, _jsonOptions, cancellationToken);
    }
}

internal sealed class SessionState
{
    public Guid? CurrentConversationId { get; set; }

    public List<Guid> RecentConversations { get; set; } = new();
}
