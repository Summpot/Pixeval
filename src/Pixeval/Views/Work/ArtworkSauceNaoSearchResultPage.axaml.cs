using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Misaki;
using Pixeval.I18N;
using Pixeval.Native.SauceNao;
using Pixeval.Utilities;
using Pixeval.Views.Viewers;

namespace Pixeval.Views.Work;

public partial class ArtworkSauceNaoSearchResultPage : ContentPage
{
    public ArtworkSauceNaoSearchResultPage(string apiKey, ReadOnlyMemory<byte> file)
    {
        InitializeComponent();
        _ = PostAsync(apiKey, file);
    }

    public async Task PostAsync(string apiKey, ReadOnlyMemory<byte> file)
    {
        var results = GetResults(apiKey, file);
        WorkContainer.ResetEngine(results);
    }

    public async IAsyncEnumerable<IArtworkInfo> GetResults(string apiKey, ReadOnlyMemory<byte> file)
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
