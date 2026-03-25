using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using Pixeval.AppManagement;
using Pixeval.I18N;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views;
using Pixeval.Views.Capability;
using Pixeval.Views.ViewContainers;

namespace Pixeval;

public class App : Application
{
    /// <summary>
    /// 确保随时能记录日志
    /// </summary>
    private FileLogger Logger { get; } = new(AppInfo.LogsFolder);

    private ViewContainerBase? RootViewContainer { get; set; }

    public override void Initialize()
    {
        RegisterUnhandledExceptionHandler();
        AppViewModel = new AppViewModel(this, Logger);
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = LanguageHelper.FindClosest(AppViewModel.AppSettings.CultureName);
        I18NManager.Register(new JsonMarkdownLangPlugin(), LanguageHelper.DefaultLanguage);
        AppViewModel.Initialize();
        AppViewModel.InitializeProvider();
        
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public static AppViewModel AppViewModel { get; private set; } = null!;

    public override void OnFrameworkInitializationCompleted()
    {
        ViewContainerBase? viewContainer = null;

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                DisableAvaloniaDataAnnotationValidation();

                desktop.Exit += (_, _) =>
                {
                    AppInfo.SaveContext();
                    AppInfo.Dispose();
                };

                viewContainer = new TabViewContainer();
                desktop.MainWindow = new Window { Content = viewContainer }
                    .Init(
                        AppInfo.AppIdentifier,
                        AppInfo.IconApplicationUri,
                        AppViewModel.AppSettings.WindowWidth,
                        AppViewModel.AppSettings.WindowHeight,
                        800,
                        450,
                        AppViewModel.AppSettings.IsMaximized);

                break;
            case ISingleViewApplicationLifetime singleViewPlatform:
                singleViewPlatform.MainView = viewContainer = new SingleViewContainer
                {
                    DataContext = new MainViewModel()
                };
                break;
        }

        RootViewContainer = viewContainer;
        RegisterProtocolActivationHandler();

        if (viewContainer is not null)
        {
            _ = LoginAsync(viewContainer);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task LoginAsync(ViewContainerBase viewContainer)
    {
        var loginContext = AppViewModel.LoginContext;
        var token = loginContext.CurrentRefreshToken;
        if (!string.IsNullOrWhiteSpace(token)
            && loginContext.Users.ContainsKey(token))
        {
            if (await AppViewModel.LoginWithRefreshTokenAsync(token))
            {
                viewContainer.NavigateTo<RecommendWorksPage>();
                return;
            }
        }
        viewContainer.NavigateTo<LoginPage>();
    }

    private void RegisterProtocolActivationHandler()
    {
        ProtocolActivationHub.UriActivated += ProtocolActivationHubOnUriActivated;

        if (this.TryGetFeature<IActivatableLifetime>() is { } activatableLifetime)
        {
            activatableLifetime.Activated += (_, args) =>
            {
                if (args is ProtocolActivatedEventArgs protocolActivatedEventArgs)
                    ProtocolActivationHub.Publish(protocolActivatedEventArgs.Uri);
            };
        }

        foreach (var uri in ProtocolActivationHub.DrainPendingUris())
            ProtocolActivationHubOnUriActivated(null, uri);
    }

    private void ProtocolActivationHubOnUriActivated(object? sender, Uri uri)
    {
        Dispatcher.UIThread.Post(() => _ = HandleProtocolActivationSafelyAsync(uri));
    }

    private async Task HandleProtocolActivationSafelyAsync(Uri uri)
    {
        try
        {
            await HandleProtocolActivationAsync(uri);
        }
        catch (Exception e)
        {
            Logger.LogError($"Failed to process protocol activation for {FormatProtocolUriForLogging(uri)}", e);
        }
    }

    private async Task HandleProtocolActivationAsync(Uri uri)
    {
        if (!AppViewModel.BrowserLoginService.IsPixivCallbackUri(uri))
            return;

        Logger.LogInformation($"Received protocol activation for {FormatProtocolUriForLogging(uri)}", null);

        if (await AppViewModel.LoginWithProtocolCallbackAsync(uri))
        {
            Logger.LogInformation($"Completed protocol activation for {FormatProtocolUriForLogging(uri)}", null);
            RootViewContainer?.NavigateTo<RecommendWorksPage>(true);
        }
    }

    private static string FormatProtocolUriForLogging(Uri uri)
    {
        return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
    }

    /// <summary>
    /// Avoid duplicate validations from both Avalonia and the CommunityToolkit.<br/>
    /// More info: <seealso href="https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins"/>
    /// </summary>
    private static void DisableAvaloniaDataAnnotationValidation()
    {
        //// Get an array of plugins to remove
        //var dataValidationPluginsToRemove =
        //    BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        //// remove each entry found
        //foreach (var plugin in dataValidationPluginsToRemove)
        //{
        //    BindingPlugins.DataValidators.Remove(plugin);
        //}
    }

    private void RegisterUnhandledExceptionHandler()
    {
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Logger.LogError(nameof(TaskScheduler.UnobservedTaskException), e.Exception);
            e.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.IsTerminating)
                Logger.LogCritical(nameof(AppDomain.UnhandledException), e.ExceptionObject as Exception);
            else
                Logger.LogError(nameof(AppDomain.UnhandledException), e.ExceptionObject as Exception);
        };
    }
}
