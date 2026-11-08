using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Pixeval.SauceNao;
using Pixeval.Pixiv;
using Pixeval.Localization;

using Pixeval.Utilities;
using Pixeval.Views.Viewers;

namespace Pixeval.Views.Works;

public partial class ArtworkSauceNaoSearchResultPage : ContentPage, IDisposable
{
    private CancellationTokenSource? _requestCancellation;
    private string _apiKey = "";
    private ReadOnlyMemory<byte> _file;
    private int _requestVersion;
    private bool _isSearching;
    private bool _reloadOnAttach;
    private bool _isDisposed;
    private readonly Pixeval.Maho.IMahoTransport? _transport;

    public ArtworkSauceNaoSearchResultPage() => InitializeComponent();

    public ArtworkSauceNaoSearchResultPage(string apiKey, ReadOnlyMemory<byte> file)
        : this(apiKey, file, null) { }

    internal ArtworkSauceNaoSearchResultPage(string apiKey, ReadOnlyMemory<byte> file, Pixeval.Maho.IMahoTransport? transport)
    {
        _transport = transport;
        InitializeComponent();
        _ = PostAsync(apiKey, file);
    }

    public Task PostAsync(string apiKey, ReadOnlyMemory<byte> file)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        _requestCancellation?.Cancel();
        _requestCancellation?.Dispose();
        _requestCancellation = new();
        ++_requestVersion;
        _apiKey = apiKey;
        _file = file;
        _reloadOnAttach = false;
        // Include a source whose first incremental-load callback has not run yet.
        _isSearching = true;
        FailurePanel.IsVisible = false;
        WorkContainer.IsVisible = true;
        WorkContainer.ResetSource(GetResults(apiKey, file, _requestCancellation.Token), SimpleWorkKind.Illustration);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<object> GetResults(string apiKey, ReadOnlyMemory<byte> file,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var version = _requestVersion;
        _isSearching = true;
        try
        {
            IReadOnlyList<SauceNaoItem>? sauceNaoResults = null;
            string? error = null;
            try
            {
                using var client = new SauceNaoClient(apiKey, null);
                if (_transport is not null) client.SetTransport(_transport);
                sauceNaoResults = await client.SearchOnceAsync(file.ToArray(), token);
                token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                yield break;
            }
            catch (SauceNaoError.ApiError ex)
            {
                error = ex.Message;
            }
            catch (Exception)
            {
                // Do not display an exception URI: the SauceNao query contains the API key.
                error = Strings.SearchSauceNAORequestFailed();
            }

            if (error is not null)
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested || _isDisposed || version != _requestVersion)
                        return;
                    SearchError.Text = error;
                    FailurePanel.IsVisible = true;
                    WorkContainer.IsVisible = false;
                });

            if (sauceNaoResults is not null)
                foreach (var result in sauceNaoResults)
                {
                    token.ThrowIfCancellationRequested();
                    var artwork = !string.IsNullOrEmpty(result.ArtworkId)
                        ? await ViewerHelper.TryGetArtworkAsync(result.Platform, result.ArtworkId,
                            this.GetRequiredUiService<MakoClient>(), this.GetRequiredUiService<BooruClient>(),
                            this.GetUiService<FileLogger>(), token)
                        : null;
                    token.ThrowIfCancellationRequested();
                    if (artwork is not null)
                        yield return artwork;
                    else if (!string.IsNullOrWhiteSpace(result.SourceUrl) || result.ExtUrls.Count > 0)
                        yield return result;
                }
        }
        finally
        {
            if (version == _requestVersion)
                _isSearching = false;
        }
    }

    private void RetryButton_OnClick(object? sender, RoutedEventArgs e) => _ = PostAsync(_apiKey, _file);

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        RaiseEvent(new ViewModelDisposalEventArgs(ViewModelDisposal.ViewModelDisposalEvent, this));
        if (_reloadOnAttach && !_isDisposed)
            _ = PostAsync(_apiKey, _file);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        _reloadOnAttach = _isSearching;
        _requestCancellation?.Cancel();
        base.OnUnloaded(e);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;
        _requestCancellation?.Cancel();
        _requestCancellation?.Dispose();
        _requestCancellation = null;
        _file = ReadOnlyMemory<byte>.Empty;
        _apiKey = "";
    }
}
