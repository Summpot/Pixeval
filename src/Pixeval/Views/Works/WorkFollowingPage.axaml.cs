// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Interactivity;
using Pixeval.Config;
using Pixeval.Controls;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Views.Works;

public partial class WorkFollowingPage : IconContentPage
{
    private readonly MakoClient _client;

    public WorkFollowingPage(AppSettings settings, MakoClient client)
        : this(settings.SearchSettings.DefaultSimpleWorkType, PrivacyPolicy.Public, client)
    {
    }

    public WorkFollowingPage(SimpleWorkType simpleWorkType, PrivacyPolicy privacyPolicy, MakoClient client, IWorkViewViewModel? viewModel = null)
    {
        _client = client;
        InitializeComponent();
        SimpleWorkTypeComboBox.SelectedValue = simpleWorkType;
        PrivacyPolicyComboBox.SelectedValue = privacyPolicy;
        if (viewModel is not null)
            WorkContainer.SetViewModel(viewModel);
        else
            ChangeSource();
    }

    private void WorkTypeComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e)
    {
        ChangeSource();
    }

    private void WorkContainer_OnRefreshRequested(object? sender, RoutedEventArgs e)
    {
        ChangeSource();
    }

    private void ChangeSource()
    {
        var workType = SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>();
        var privacy = PrivacyPolicyComboBox.GetSelectedValue<PrivacyPolicy>();
        var engine = _client.WorkFollowing(workType, privacy);
        WorkContainer.ResetSource(engine, workType);
    }
}
