using System.Threading.Tasks;
using Avalonia;
using Pixeval.Common;
using Avalonia.Browser;
using Pixeval;
using Pixeval.Utilities;

internal sealed partial class Program
{
    private static Task Main(string[] args) => BuildAvaloniaApp()
        .WithPixevalFonts()
#if DEBUG
        .WithDeveloperTools()
#endif
        .StartBrowserAppAsync("out");

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
