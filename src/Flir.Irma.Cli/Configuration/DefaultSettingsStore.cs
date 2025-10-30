using System.Text.Json;

namespace Flir.Irma.Cli.Configuration;

internal sealed class DefaultSettingsStore
{
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private readonly string _filePath;

    public DefaultSettingsStore(string? filePath = null)
    {
        _filePath = filePath ?? IrmaCliPaths.DefaultsFilePath;
    }

    public async Task<IDictionary<string, string>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        await using var stream = File.OpenRead(_filePath);
        var dictionary = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, _serializerOptions, cancellationToken)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return new Dictionary<string, string>(dictionary, StringComparer.OrdinalIgnoreCase);
    }

    public async Task SaveAsync(IDictionary<string, string> values, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, values, _serializerOptions, cancellationToken);
    }
}
