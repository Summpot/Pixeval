using System;
using Avalonia;

namespace Pixeval.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (DesktopProtocolRegistrar.TryRelaunchAsMacBundle(args))
            return;

        using var macOsStartupProtocolBridge = MacOSStartupProtocolBridge.TryInstall();

        if (!DesktopProtocolRelay.Initialize(args))
            return;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
