namespace Flir.Irma.Cli.Configuration;

internal static class IrmaCliPaths
{
    private const string RootFolderName = ".irma";
    private static readonly Lazy<string> BaseDirectoryLazy = new(() => EnsureDirectory(RootFolderName));

    public static string BaseDirectory => BaseDirectoryLazy.Value;

    public static string DefaultsFilePath => Path.Combine(BaseDirectory, "defaults.json");

    public static string AuthFilePath => Path.Combine(BaseDirectory, "auth.json");

    public static string SessionsFilePath => Path.Combine(BaseDirectory, "sessions.json");

    public static string LogsDirectory => EnsureDirectory(Path.Combine(BaseDirectory, "logs"));

    private static string EnsureDirectory(string relativeOrAbsolute)
    {
        var fullPath = Path.IsPathRooted(relativeOrAbsolute)
            ? relativeOrAbsolute
            : Path.Combine(GetHomeDirectory(), relativeOrAbsolute);

        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }

        return fullPath;
    }

    private static string GetHomeDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            return home;
        }

        home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrWhiteSpace(home))
        {
            return home;
        }

        throw new InvalidOperationException("Unable to resolve user home directory for Irma CLI configuration storage.");
    }
}
