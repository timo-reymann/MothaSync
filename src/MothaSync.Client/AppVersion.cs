using System.Reflection;

namespace MothaSync.Client;

public static class AppVersion
{
    public static string Current { get; } = Format(
        typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0");

    public static string MenuHeader => $"Version v{Current}";

    private static string Format(string version)
    {
        var separator = version.IndexOf('+');
        if (separator < 0 || version.Length - separator - 1 <= 7)
        {
            return version;
        }

        return version[..(separator + 1)] + version[(separator + 1)..(separator + 8)];
    }
}
