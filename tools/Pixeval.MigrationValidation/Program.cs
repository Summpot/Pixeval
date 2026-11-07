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
        if (args.Length is < 2 or > 3
            || args[0] is not ("inventory" or "capture" or "benchmark" or "navigation" or "extensions" or "search" or "search-options" or "search-page" or "login" or "work-selection" or "extension-management" or "bookmark-tags" or "bookmark-page" or "ranking-page" or "viewer-toolbar" or "documentation" or "dialogs" or "ui")
            || args[0] is "extensions" && args.Length is not 3
            || args.Length is 3 && args[0] is not ("extensions" or "ui"))
        {
            Console.Error.WriteLine("Usage: Pixeval.MigrationValidation <inventory|capture|benchmark|navigation|extensions|search|search-options|search-page|login|work-selection|extension-management|bookmark-tags|bookmark-page|ranking-page|viewer-toolbar|documentation|dialogs|ui> <new evidence directory> [native fixture library: required for extensions, optional for ui]");
            return 1;
        }

        var outputDirectory = Path.GetFullPath(args[1]);
        if (Directory.Exists(outputDirectory) || File.Exists(outputDirectory))
            throw new IOException($"Use a new evidence directory: {outputDirectory}");
        _ = Directory.CreateDirectory(outputDirectory);

        // Set before AppInfo or App construction. Neither mode reads the user's accounts,
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
        if (args.Length is 3)
        {
            var library = Path.GetFullPath(args[2]);
            if (!File.Exists(library))
                throw new FileNotFoundException("The native fixture library does not exist.", library);
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

        if (args[0] is "capture" or "benchmark" or "navigation" or "extensions" or "search" or "search-options" or "search-page" or "login" or "work-selection" or "extension-management" or "bookmark-tags" or "bookmark-page" or "ranking-page" or "viewer-toolbar" or "documentation" or "dialogs")
        {
            RunHeadlessAsync(args[0], outputDirectory).GetAwaiter().GetResult();
            return 0;
        }

        _ = AppBuilder.Configure<InventoryApplication>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .WithPixevalFonts()
            .SetupWithoutStarting();
        try
        {
            WriteValidationContext(outputDirectory);
            MigrationInventory.Export(Path.Combine(outputDirectory, "inventory.json"));
            return 0;
        }
        finally
        {
            App.AppViewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
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
                    case "capture":
                        await BaselineCapture.ExportAsync(directory);
                        break;
                    case "benchmark":
                        await PerformanceBaseline.ExportAsync(directory);
                        break;
                    case "navigation":
                        await NavigationEditorValidation.RunAsync(directory);
                        break;
                    case "extensions":
                        await NativeExtensionValidation.RunAsync(directory);
                        break;
                    case "search":
                        await SearchCompletionValidation.RunAsync(directory);
                        break;
                    case "search-options":
                        await SearchArgumentsValidation.RunAsync(directory);
                        break;
                    case "search-page":
                        await SearchPageValidation.RunAsync(directory);
                        break;
                    case "login":
                        await LoginLifetimeValidation.RunAsync(directory);
                        break;
                    case "work-selection":
                        await WorkSelectionValidation.RunAsync(directory);
                        break;
                    case "extension-management":
                        await ExtensionManagementValidation.RunAsync(directory);
                        break;
                    case "bookmark-tags":
                        await BookmarkTagsValidation.RunAsync(directory);
                        break;
                    case "bookmark-page":
                        await BookmarksPageValidation.RunAsync(directory);
                        break;
                    case "ranking-page":
                        await RankingPageValidation.RunAsync(directory);
                        break;
                    case "viewer-toolbar":
                        await ViewerToolbarValidation.RunAsync(directory);
                        break;
                    case "documentation":
                        await DocumentationValidation.RunAsync(directory);
                        break;
                    case "dialogs":
                        await DialogValidation.RunAsync(directory);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mode));
                }
                return true;
            }
            finally
            {
                await App.AppViewModel.DisposeAsync();
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
        // Metadata export initializes the real settings registration but never navigates,
        // logs in, starts MCP, loads artwork or installs a native window.
        public override void OnFrameworkInitializationCompleted()
        {
        }

        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<InventoryApplication>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false, ShouldRenderOnUIThread = true })
            .WithPixevalFonts();
    }
}
