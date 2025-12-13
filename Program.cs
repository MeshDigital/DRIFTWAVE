using System;

namespace SLSKDONET;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
#if NET8_0_WINDOWS
        // WPF entry point for Windows
        var app = new App();
        app.InitializeComponent();
        app.Run();
#else
        // Avalonia entry point for cross-platform
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
#endif
    }

#if !NET8_0_WINDOWS
    // Avalonia configuration, don't remove; also used by visual designer.
    public static Avalonia.AppBuilder BuildAvaloniaApp()
        => Avalonia.AppBuilder.Configure<AvaloniaApp>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
#endif
}
