// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using Pixeval.Pixiv;
using Pixeval.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Views.Works;

public partial class SpotlightPage : IconContentPage
{
    private readonly MakoClient _client;

    public SpotlightPage(MakoClient client) : this(null, client)
    {
    }

    public SpotlightPage(SpotlightViewViewModel? viewModel, MakoClient client)
    {
        _client = client;
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
        ResetEngine(_client.Spotlight());
    }

    private void ResetEngine(IAsyncEnumerable<SpotlightArticle> fetchEngine) =>
        (SpotlightView.DataContext as SpotlightViewViewModel)?.ResetEngine(fetchEngine);
}
