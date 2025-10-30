using System.Reflection;
using System.Runtime.InteropServices;

namespace Flir.Irma.WebApi.Infrastructure;

public class ApplicationMetadata
{
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private readonly Lazy<string> _informationalVersion = new(() =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0-dev");

    public DateTimeOffset StartedAt => _startedAt;

    public string Version => _informationalVersion.Value;

    public string Commit
    {
        get
        {
            var metadata = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => string.Equals(a.Key, "GitCommit", StringComparison.OrdinalIgnoreCase));
            if (metadata is not null)
            {
                return metadata.Value;
            }

            // Attempt to parse commit from informational version (e.g. 1.0.0+sha.9f2c5ad)
            var parts = Version.Split('+', 2);
            if (parts.Length == 2)
            {
                var suffix = parts[1];
                if (suffix.StartsWith("sha.", StringComparison.OrdinalIgnoreCase))
                {
                    return suffix["sha.".Length..];
                }
            }

            return "unknown";
        }
    }

    public string BuildDate
    {
        get
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            return File.Exists(assemblyLocation)
                ? File.GetLastWriteTimeUtc(assemblyLocation).ToString("O")
                : DateTime.UtcNow.ToString("O");
        }
    }

    public string Runtime => RuntimeInformation.FrameworkDescription;
}
