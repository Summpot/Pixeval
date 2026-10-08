// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Views.Capability;

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
        ResetEngine(App.AppViewModel.MakoClient.Spotlight().ToFetchEngine());
    }

    private void ResetEngine(IFetchEngine<SpotlightArticle> fetchEngine) =>
        (SpotlightView.DataContext as SpotlightViewViewModel)?.ResetEngine(fetchEngine);
}
