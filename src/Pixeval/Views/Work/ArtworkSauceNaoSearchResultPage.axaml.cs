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
using Misaki;
using Pixeval.I18N;
using Pixeval.Models.SauceNao;
using Pixeval.Utilities;
using Pixeval.Views.Viewers;

namespace Pixeval.Views.Work;

public partial class ArtworkSauceNaoSearchResultPage : ContentPage, IDisposable
{
    private readonly HttpClient _httpClient;
    private CancellationTokenSource? _requestCancellation;
    private string _apiKey = "";
    private ReadOnlyMemory<byte> _file;
    private int _requestVersion;
    private bool _isSearching;
    private bool _reloadOnAttach;
    private bool _isDisposed;

    public ArtworkSauceNaoSearchResultPage(string apiKey, ReadOnlyMemory<byte> file, HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? App.AppViewModel.GetRequiredHttpClient();
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
        WorkContainer.ResetEngine(GetResults(apiKey, file, _requestCancellation.Token));
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<IArtworkInfo> GetResults(string apiKey, ReadOnlyMemory<byte> file,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var version = _requestVersion;
        _isSearching = true;
        try
        {
            IReadOnlyList<SauceNaoResult>? sauceNaoResults = null;
            string? error = null;
            try
            {
                using var form = new MultipartFormDataContent();
                using var fileContent = new ReadOnlyMemoryContent(file);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                form.Add(fileContent, "file", "img");
                using var response = await _httpClient.PostAsync(new SauceNaoRequest(apiKey).ToQueryString(), form, token);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync(SauceNaoResponseSerializerContext.Default.SauceNaoResponse, token);
                token.ThrowIfCancellationRequested();
                if (result is { Header.Status: 0, Results: { } results })
                    sauceNaoResults = results;
                else
                    error = string.IsNullOrWhiteSpace(result?.Header.Message)
                        ? I18NManager.GetResource(SearchResources.Validation.SearchFailed.Title)
                        : result.Header.Message;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                yield break;
            }
            catch (Exception)
            {
                // Do not display an exception URI: the SauceNao query contains the API key.
                error = I18NManager.GetResource(SearchResources.SauceNAO.RequestFailed);
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
                    if (result?.Data?.ToIdentityInfo() is not { } identityInfo)
                        continue;
                    var artwork = await identityInfo.TryGetArtworkInfoAsync(token);
                    token.ThrowIfCancellationRequested();
                    if (artwork is not null)
                        yield return artwork;
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
