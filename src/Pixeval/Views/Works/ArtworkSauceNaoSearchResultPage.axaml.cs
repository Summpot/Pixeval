using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Pixeval.Localization;
using Pixeval.Pixiv;
using Pixeval.SauceNao;
using Pixeval.Utilities;
using Pixeval.Views.Viewers;

namespace Pixeval.Views.Works;

public partial class ArtworkSauceNaoSearchResultPage : ContentPage
{
    public ArtworkSauceNaoSearchResultPage()
    {
        InitializeComponent();
    }

    public ArtworkSauceNaoSearchResultPage(string apiKey, ReadOnlyMemory<byte> file)
    {
        InitializeComponent();
        _ = PostAsync(apiKey, file);
    }

    public async Task PostAsync(string apiKey, ReadOnlyMemory<byte> file)
    {
        var results = GetResults(apiKey, file);
        WorkContainer.ResetSource(results, SimpleWorkType.Illustration);
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
            this.Notifications.ShowError(Strings.MiscExceptionEncountered(), e.Message);
        }

        if (sauceNaoResults is not null)
        {
            foreach (var result in sauceNaoResults)
            {
                if (!string.IsNullOrEmpty(result.ArtworkId)
                    && await ViewerHelper.TryGetArtworkAsync(
                        result.Platform,
                        result.ArtworkId,
                        this.GetRequiredUiService<MakoClient>(),
                        this.GetRequiredUiService<BooruClient>(),
                        this.GetUiService<FileLogger>()) is { } artwork)
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
