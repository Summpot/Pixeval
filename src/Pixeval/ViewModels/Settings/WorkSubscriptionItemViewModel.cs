// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.AppManagement;
using Pixeval.Controls;
using Pixeval.Native.Mako;
using Pixeval.Native.Storage;

namespace Pixeval.ViewModels.Settings;

public sealed partial class WorkSubscriptionItemViewModel(WorkSubscriptionRecord entry) : ViewModelBase
{
    public WorkSubscriptionRecord Entry { get; private set; } = entry;

    [ObservableProperty] public partial User User { get; private set; } = CreateUser(entry);

    public string SubscriptionTypeText => SymbolComboBoxItem.GetResource(Entry.Type);

    public string WorkKindText => SymbolComboBoxItem.GetResource(Entry.Kind);

    internal void UpdateSubscription(WorkSubscriptionRecord subscription)
    {
        Entry = subscription;
        User = CreateUser(Entry);
    }

    private static User CreateUser(WorkSubscriptionRecord entry) => new(
        entry.Id,
        entry.DisplayName,
        entry.Account,
        new ProfileImageUrls(null, null, null, string.IsNullOrWhiteSpace(entry.AvatarUrl) ? AppInfo.ImageNotAvailablePath : entry.AvatarUrl),
        false,
        null);
}
