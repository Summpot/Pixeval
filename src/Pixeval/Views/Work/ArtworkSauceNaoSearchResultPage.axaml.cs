using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Misaki;
using Pixeval.I18N;
using Pixeval.Native.SauceNao;
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
        var viewContainer = TopLevel.GetTopLevel(this)?.ViewContainer;

        List<SauceNaoItem>? sauceNaoResults = null;
        try
        {
            using var client = new SauceNaoClient(apiKey, null);
            sauceNaoResults = await client.SearchAsync(file.ToArray());
        }
        catch (Exception e)
        {
            viewContainer?.ShowError(I18NManager.GetResource(MiscResources.ExceptionEncountered), e.Message);
        }

        if (sauceNaoResults is not null)
        {
            foreach (var result in sauceNaoResults)
            {
                if (result.ToIdentityInfo() is { } identityInfo
                    && await identityInfo.TryGetArtworkInfoAsync() is { } artwork)
                {
                    yield return artwork;
                }
                else
                {
                    yield return result;
                }
            }
        }
    }
}
