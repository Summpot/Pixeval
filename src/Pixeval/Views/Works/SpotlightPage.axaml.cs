// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Microsoft.Extensions.DependencyInjection;
using Pixeval.Pixiv;
using Pixeval.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Views.Works;

public partial class SpotlightPage : IconContentPage
{
    public SpotlightPage() : this(null)
    {
    }

    public SpotlightPage(SpotlightViewViewModel? viewModel)
    {
        InitializeComponent();
        if (viewModel is not null)
            SpotlightView.SetViewModel(viewModel);
        else
        {
            ChangeSource();
        }
    }

    private void ChangeSource()
    {
        ResetEngine(App.Services!.GetRequiredService<MakoClient>().Spotlight().ToFetchEngine());
    }

    private void ResetEngine(IFetchEngine<SpotlightArticle> fetchEngine) =>
        (SpotlightView.DataContext as SpotlightViewViewModel)?.ResetEngine(fetchEngine);
}
