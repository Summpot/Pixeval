// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading.Tasks;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;

namespace Pixeval.Services.Core;

/// <summary>
/// Inheritable attached property that carries the application service provider down the visual tree.
/// The shell (<see cref="Views.Shell.TabViewContainer"/>) sets it once when it is constructed; test windows set it themselves.
/// Templated controls that cannot be constructor-injected read their UI services from here instead of crawling
/// <c>TopLevel.GetTopLevel(this)?.ViewContainer</c> or calling <c>App.Services</c>.
/// </summary>
public static class UiServices
{
    public static readonly AttachedProperty<IServiceProvider?> ProviderProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, IServiceProvider?>("Provider", typeof(UiServices), inherits: true);

    public static IServiceProvider? GetProvider(AvaloniaObject element) => element.GetValue(ProviderProperty);

    public static void SetProvider(AvaloniaObject element, IServiceProvider? value) => element.SetValue(ProviderProperty, value);

    extension(AvaloniaObject element)
    {
        public T? GetUiService<T>() where T : class => GetProvider(element)?.GetService<T>();

        /// <summary>
        /// Resolves a required service from the inherited provider. Only valid from interaction handlers,
        /// i.e. after the element has been attached under the shell.
        /// </summary>
        public T GetRequiredUiService<T>() where T : class =>
            (GetProvider(element) ?? throw new InvalidOperationException(
                $"{element.GetType().Name} is not attached under a UI host; {typeof(T).Name} cannot be resolved."))
            .GetRequiredService<T>();

        public INotificationService Notifications => element.GetUiService<INotificationService>() ?? NullNotificationService.Instance;

        public IDialogService Dialogs => element.GetUiService<IDialogService>() ?? NullDialogService.Instance;

        public IClipboardService ClipboardService => element.GetUiService<IClipboardService>() ?? NullClipboardService.Instance;

        public IStoragePickerService StoragePicker => element.GetUiService<IStoragePickerService>() ?? NullStoragePickerService.Instance;

        public ILauncherService LauncherService => element.GetUiService<ILauncherService>() ?? NullLauncherService.Instance;

        /// <summary>
        /// Runs an asynchronous UI interaction started from a synchronous framework callback (event handler,
        /// <c>OnLoaded</c>) without <c>async void</c>: failures are logged and surfaced through
        /// <see cref="INotificationService"/> instead of tearing down the process.
        /// </summary>
        public void RunInteraction(Func<Task> interaction, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
        {
            _ = RunAsync();

            async Task RunAsync()
            {
                try
                {
                    await interaction();
                }
                catch (OperationCanceledException)
                {
                    // cancelled interactions are not failures
                }
                catch (Exception e)
                {
                    element.GetUiService<FileLogger>()?.LogError($"{element.GetType().Name}.{caller}", e);
                    element.Notifications.ShowError(e.GetType().Name, e.Message);
                }
            }
        }

        public INavigationService? AppNavigation => element.GetUiService<INavigationService>();
    }
}
