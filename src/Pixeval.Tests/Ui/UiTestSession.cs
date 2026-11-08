using System;
using System.IO;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Headless;
using Mako;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.AppManagement;
using Pixeval.AppManagement.Settings;
using Pixeval.Utilities;
using Pixeval.Views.ViewContainers;

namespace Pixeval.Tests.Ui;

internal static class UiTestSession
{
    internal static async Task RunAsync(TestContext context, Func<string, Task> test,
        string culture = "zh-Hans", bool nativeExtension = false)
    {
        var fixture = Environment.GetEnvironmentVariable("PIXEVAL_TEST_EXTENSION_FIXTURE");
        if (nativeExtension && string.IsNullOrEmpty(fixture))
            Assert.Inconclusive("Set PIXEVAL_TEST_EXTENSION_FIXTURE to the pinned native fixture library.");

        var previous = AppContext.GetData(ApplicationDataDirectory.OverrideKey);
        var results = Environment.GetEnvironmentVariable("PIXEVAL_TEST_ARTIFACTS") ?? context.TestResultsDirectory!;
        var directory = Path.Combine(results, "ui", context.TestName!, Guid.NewGuid().ToString("N"));
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
            await using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApplication));
            await session.DispatchAsync(async () =>
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

    public sealed class UiTestApplication : App
    {
        protected internal override void ConfigureServices(IServiceCollection services) =>
            services.AddSingleton<IWindowActivationService, OfflineWindowActivation>();

        protected internal override MakoClient CreateMakoClient(MakoConfiguration configuration, FileLogger logger)
        {
            var client = base.CreateMakoClient(configuration, logger);
            OfflineHttpTransport.Install(client);
            return client;
        }

        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<UiTestApplication>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false, ShouldRenderOnUIThread = true })
            .WithPixevalFonts();
    }

    private sealed class OfflineWindowActivation : IWindowActivationService
    {
        public Task ActivateAsync(ViewContainerBase container) => Task.CompletedTask;
    }
}
