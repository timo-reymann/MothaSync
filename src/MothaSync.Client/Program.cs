using Avalonia;
using System;

namespace MothaSync.Client;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        RcloneBootstrap.EnsureInstalled();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .LogToTrace();
}
