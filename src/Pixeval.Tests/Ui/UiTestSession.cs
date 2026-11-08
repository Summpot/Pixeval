using System;
using System.IO;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Headless;
using Pixeval.Mako;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Services.Core;
using Pixeval.Config;
using Pixeval.Utilities;
using Pixeval.Views.Shell;

namespace Pixeval.Tests.Ui;

internal static class UiTestSession
{
    // Headless creates and tears down a fresh App/services scope per dispatch. Keep
    // its worker thread for the assembly: AvaloniaEdit caches dispatcher-affine
    // KeyBindings for the process, as it does in the single-threaded desktop app.
    internal static T Create<T>(params object[] arguments) where T : class =>
        ActivatorUtilities.CreateInstance<T>(App.AppViewModel.AppServiceProvider, arguments);

    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() =>
        HeadlessUnitTestSession.StartNew(typeof(UiTestApplication), AvaloniaTestIsolationLevel.PerTest));

    internal static ValueTask DisposeAsync() => Session.IsValueCreated ? Session.Value.DisposeAsync() : ValueTask.CompletedTask;

    internal static async Task RunAsync(TestContext context, Func<string, Task> test,
        string culture = "zh-Hans", bool nativeExtension = false)
    {
        var fixture = Environment.GetEnvironmentVariable("PIXEVAL_TEST_EXTENSION_FIXTURE");
        if (nativeExtension && string.IsNullOrEmpty(fixture))
            Assert.Inconclusive("Set PIXEVAL_TEST_EXTENSION_FIXTURE to the pinned native fixture library.");

        var previous = AppContext.GetData(ApplicationDataDirectory.OverrideKey);
        var results = Environment.GetEnvironmentVariable("PIXEVAL_TEST_ARTIFACTS") ?? context.TestResultsDirectory!;
        var directory = Path.GetFullPath(Path.Combine(results, "ui", context.TestName!, Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        AppContext.SetData(ApplicationDataDirectory.OverrideKey, Path.Combine(directory, "data"));
        try
        {
            AppInfo.InitializeDataDirectories();
            var settings = new AppSettings();
            settings.ApplicationSettings.CultureName = culture;
            settings.DownloadSettings.DownloadPathMacro = Path.Combine(AppInfo.ApplicationFolderPath, "Downloads", "@{id}.@{ext}");
            AppInfo.SaveAppSettings(settings);
            if (nativeExtension)
                File.Copy(fixture!, Path.Combine(AppInfo.ExtensionsFolder, Path.GetFileName(fixture!)));
            await Session.Value.DispatchAsync(async () =>
            {
                try
                {
                    Assert.AreEqual(culture, CultureInfo.CurrentUICulture.Name, "The requested test culture must be active.");
                    File.WriteAllLines(Path.Combine(directory, "test-context.txt"),
                    [
                        $"Culture: {CultureInfo.CurrentUICulture.Name}",
                        $"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}",
                        $"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}"
                    ]);
                    await test(directory);
                }
                finally { await ((App) Application.Current!).DisposeAsync(); }
            });
        }
        finally
        {
            AppContext.SetData(ApplicationDataDirectory.OverrideKey, previous);
            foreach (var file in Directory.EnumerateFiles(directory))
                context.AddResultFile(file);
        }
    }

    public sealed class UiTestApplication
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure(() => new App
            {
                EnableDeveloperTools = false,
                ConfigureServices = services => services.AddSingleton<IWindowActivationService, OfflineWindowActivation>().AddSingleton<Pixeval.Services.Update.IAppUpdateNotificationCoordinator, OfflineUpdates>(),
                ConfigureTransport = (mako, maho, images, downloads) => { OfflineHttpTransport.Install(mako, maho, images.ImageCache); downloads.SetTransport(OfflineHttpTransport.For(mako)); }
            })
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false, ShouldRenderOnUIThread = true })
            .WithPixevalFonts();
    }

    private sealed class OfflineUpdates : Pixeval.Services.Update.IAppUpdateNotificationCoordinator
    {
        public Task CheckAndNotifyUpdatesAsync() => Task.CompletedTask;
    }

    private sealed class OfflineWindowActivation : IWindowActivationService
    {
        public Task ActivateAsync(ViewContainerBase container) => Task.CompletedTask;
    }
}
