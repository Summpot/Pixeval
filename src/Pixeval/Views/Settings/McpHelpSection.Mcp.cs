// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Interactivity;
using Pixeval.Config;
using Pixeval.Localization;
using Pixeval.Services.Background;

namespace Pixeval.Views.Settings;

public partial class McpHelpSection
{
    public static readonly DirectProperty<McpHelpSection, string> McpToolsStatusProperty =
        AvaloniaProperty.RegisterDirect<McpHelpSection, string>(nameof(McpToolsStatus), static section => section.McpToolsStatus);

    private bool _isLoadingMcpTools;
    private Func<string> _status = Strings.HelpPageMcpToolsStatusDisabled;
    private IDisposable? _languageSubscription;
    private void SetMcpStatus(Func<string> status)
    {
        _status = status;
        McpToolsStatus = status();
    }

    public ObservableCollection<McpToolItemViewModel> McpTools { get; } = [];

    public string McpToolsStatus
    {
        get;
        private set => SetAndRaise(McpToolsStatusProperty, ref field, value);
    } = "";

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        this.RunInteraction(ReloadMcpToolsAsync);
    }

    private void RefreshMcpToolsButton_OnClicked(object? sender, RoutedEventArgs e) =>
        this.RunInteraction(ReloadMcpToolsAsync);

    private async Task ReloadMcpToolsAsync()
    {
        if (_isLoadingMcpTools)
            return;

        var settings = this.GetRequiredUiService<AppSettings>().McpSettings;
        if (!settings.EnableServer)
        {
            McpTools.Clear();
            SetMcpStatus(() => Strings.HelpPageMcpToolsStatusDisabled());
            return;
        }

        if (this.GetUiService<IPixevalMcpService>() is not { } service)
        {
            McpTools.Clear();
            SetMcpStatus(() => Strings.HelpPageMcpToolsStatusUnavailable());
            return;
        }

        var configuredEndpoint = new Uri($"http://127.0.0.1:{settings.Port}{PixevalMcpService.DefaultPath}");
        _isLoadingMcpTools = true;
        RefreshMcpToolsButton.IsEnabled = false;
        SetMcpStatus(() => Strings.HelpPageMcpToolsStatusLoading(endpoint: configuredEndpoint));

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await service.ApplySettingsAsync(cts.Token);
            if (service.Endpoint is not { } endpoint)
            {
                McpTools.Clear();
                SetMcpStatus(() => Strings.HelpPageMcpToolsStatusNotRunning());
                return;
            }

            var tools = await RequestMcpToolsAsync(endpoint, cts.Token);
            McpTools.Clear();
            foreach (var tool in tools)
                McpTools.Add(tool);

            SetMcpStatus(() => tools.Count is 0
                ? Strings.HelpPageMcpToolsStatusEmpty()
                : Strings.HelpPageMcpToolsStatusLoaded(endpoint: endpoint, count: tools.Count));
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or InvalidOperationException or OperationCanceledException)
        {
            McpTools.Clear();
            SetMcpStatus(() => Strings.HelpPageMcpToolsStatusFailed(error: TrimErrorMessage(e.Message)));
        }
        finally
        {
            RefreshMcpToolsButton.IsEnabled = true;
            _isLoadingMcpTools = false;
        }
    }
}
