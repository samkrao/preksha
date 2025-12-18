using Avalonia;
using System;

namespace SubscriptionTracker.AvaloniaApp;

internal static class Program
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont();

    [STAThread]
    public static void Main(string[] args)
    {
        Db.Init();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
}
