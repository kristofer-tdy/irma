using Flir.Irma.Cli.Configuration;

namespace Flir.Irma.Cli.Tests;

public class DefaultSettingsStoreTests
{
    [Fact]
    public async Task SaveAndLoadRoundTrips()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var path = Path.Combine(tempDir, "defaults.json");
            var store = new DefaultSettingsStore(path);
            var cancellationToken = CancellationToken.None;

            var defaults = new Dictionary<string, string>
            {
                ["product"] = "Ixx/1.0",
                ["base-url"] = "https://localhost:5001"
            };

            await store.SaveAsync(defaults, cancellationToken);
            var reloaded = await store.LoadAsync(cancellationToken);

            Assert.Equal(defaults.Count, reloaded.Count);
            Assert.Equal(defaults["product"], reloaded["product"]);
            Assert.Equal(defaults["base-url"], reloaded["base-url"]);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
