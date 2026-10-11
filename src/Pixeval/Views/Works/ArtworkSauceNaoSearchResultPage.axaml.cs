using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Pixeval.I18N;
using Pixeval.SauceNao;
using Pixeval.Utilities;
using Pixeval.Views.Viewers;

namespace Pixeval.Views.Works;

public partial class ArtworkSauceNaoSearchResultPage : ContentPage, IDisposable
{
    public ArtworkSauceNaoSearchResultPage()
    {
        InitializeComponent();
    }

    public ArtworkSauceNaoSearchResultPage(string apiKey, ReadOnlyMemory<byte> file)
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

    public async IAsyncEnumerable<object> GetResults(string apiKey, ReadOnlyMemory<byte> file)
    {
        List<SauceNaoItem>? sauceNaoResults = null;
        try
        {
            using var client = new SauceNaoClient(apiKey, null);
            sauceNaoResults = await client.SearchAsync(file.ToArray());
        }
        catch (Exception e)
        {
            this.Notifications.ShowError(I18NManager.GetResource(MiscResources.ExceptionEncountered), e.Message);
        }

        if (sauceNaoResults is not null)
        {
            foreach (var result in sauceNaoResults)
            {
                if (!string.IsNullOrEmpty(result.ArtworkId)
                    && await ViewerHelper.TryGetArtworkAsync(result.Platform, result.ArtworkId) is { } artwork)
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
