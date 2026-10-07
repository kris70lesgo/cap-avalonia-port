using Avalonia;
using System;

namespace CapAvalonia;

internal class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect();
#if DEBUG
        builder = builder.WithDeveloperTools();
#endif
        return builder
            .ConfigureFonts(fontManager => fontManager.AddFontCollection(new GeistFontCollection()))
            .WithInterFont()
            .LogToTrace();
    }
}
