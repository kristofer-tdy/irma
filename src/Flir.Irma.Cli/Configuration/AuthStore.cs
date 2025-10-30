using System.Text.Json;

namespace Flir.Irma.Cli.Configuration;

internal sealed class AuthStore
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);
    private readonly string _filePath;

    public AuthStore(string? filePath = null)
    {
        _filePath = filePath ?? IrmaCliPaths.AuthFilePath;
    }

    public async Task<AuthTicket?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<AuthTicket>(stream, _options, cancellationToken);
    }

    public async Task SaveAsync(AuthTicket ticket, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, ticket, _options, cancellationToken);
    }

    public Task ClearAsync()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }

        return Task.CompletedTask;
    }
}

internal sealed record AuthTicket(string AccessToken, DateTimeOffset ExpiresAt, string Scheme = "Bearer")
{
    public bool IsExpired(TimeSpan? skew = null)
    {
        var buffer = skew ?? TimeSpan.FromMinutes(2);
        return DateTimeOffset.UtcNow >= ExpiresAt.Subtract(buffer);
    }
}
