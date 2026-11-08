using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Headless;
using Pixeval;
using Pixeval.AppManagement;
using Pixeval.AppManagement.Settings;
using Pixeval.I18N;
using Pixeval.Models.Options;
using Pixeval.Utilities;

namespace Pixeval.MigrationValidation;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length is < 2 or > 3 || args[0] is not ("inventory" or "capture" or "benchmark" or "ui")
            || args.Length == 3 && args[0] != "ui")
        {
            Console.Error.WriteLine("Usage: Pixeval.MigrationValidation <inventory|capture|benchmark|ui> <new output directory> [trusted native library for ui]");
            return 1;
        }

        var outputDirectory = Path.GetFullPath(args[1]);
        if (Directory.Exists(outputDirectory) || File.Exists(outputDirectory))
            throw new IOException($"Use a new evidence directory: {outputDirectory}");
        _ = Directory.CreateDirectory(outputDirectory);

        // Set before AppInfo or App construction. No mode reads the user's accounts,
        // extensions, navigation, history or download destination.
        var dataDirectory = Path.Combine(outputDirectory, "data");
        AppContext.SetData("Pixeval.ApplicationDataDirectory", dataDirectory);
        var settings = new AppSettings();
        var culture = CultureInfo.GetCultureInfo(Environment.GetEnvironmentVariable("PIXEVAL_MIGRATION_CULTURE") ?? "zh-Hans");
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        settings.ApplicationSettings.CultureName = culture.Name;
        settings.ApplicationSettings.Theme = ApplicationTheme.Light;
        settings.DownloadSettings.DownloadPathMacro = Path.Combine(dataDirectory, "Downloads", "@{id}.@{ext}");
        AppInfo.SaveAppSettings(settings);
        if (!File.Exists(Path.Combine(AppInfo.SettingsFolder, "settings.yaml")))
            throw new IOException("Could not initialize isolated settings.");
        if (args.Length == 3)
        {
            var library = Path.GetFullPath(args[2]);
            File.Copy(library, Path.Combine(AppInfo.ExtensionsFolder, Path.GetFileName(library)));
        }
        if (args[0] is "ui")
        {
            // Use the real App lifecycle and shell, with the same desktop font setup.
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .With(new SkiaOptions { MaxGpuResourceSizeBytes = 268435456 })
                .WithPixevalFonts()
                .StartWithClassicDesktopLifetime([]);
        }

        RunHeadlessAsync(args[0], outputDirectory).GetAwaiter().GetResult();
        return 0;
    }

    private static async Task RunHeadlessAsync(string mode, string directory)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(InventoryApplication));
        // An explicit result type selects the asynchronous dispatch overload; a plain
        // Func<Task> would select Dispatch<Task> and leave its inner task unobserved.
        _ = await session.Dispatch<bool>(async () =>
        {
            try
            {
                WriteValidationContext(directory);
                MigrationInventory.Export(Path.Combine(directory, "inventory.json"));
                switch (mode)
                {
                    case "inventory":
                        break;
                    case "capture":
                        await BaselineCapture.ExportAsync(directory);
                        break;
                    case "benchmark":
                        await PerformanceBaseline.ExportAsync(directory);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mode));
                }
                return true;
            }
            finally
            {
                await ((App)Application.Current!).DisposeAsync();
            }
        }, default);
    }

    private static void WriteValidationContext(string directory)
    {
        var requested = CultureInfo.GetCultureInfo(App.AppViewModel.AppSettings.ApplicationSettings.CultureName);
        if (!I18NManager.AvailableCultures.ContainsKey(requested))
            throw new ArgumentException($"The validation culture '{requested.Name}' has no installed translation.");
        if (CultureInfo.CurrentUICulture.Name != requested.Name)
            throw new InvalidOperationException($"Requested culture '{requested.Name}' changed to '{CultureInfo.CurrentUICulture.Name}'.");
        File.WriteAllLines(Path.Combine(directory, "validation-context.txt"),
        [
            $"Culture: {CultureInfo.CurrentUICulture.Name}",
            $"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}",
            $"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}"
        ]);
    }

    public sealed class InventoryApplication : App
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<InventoryApplication>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false, ShouldRenderOnUIThread = true })
            .WithPixevalFonts();
    }
}
