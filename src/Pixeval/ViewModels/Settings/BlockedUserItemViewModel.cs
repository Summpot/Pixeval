// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.Native.Mako;
using Pixeval.Native.Storage;
using Pixeval.Utilities;

namespace Pixeval.ViewModels.Settings;

public sealed partial class BlockedUserItemViewModel(BlockedUserRecord entry) : ViewModelBase
{
    public BlockedUserRecord Entry { get; private set; } = entry;

    [ObservableProperty]
    public partial User User { get; private set; } = BlockedContentModelHelper.CreateBlockedUserPreview(entry);

    internal void UpdateUser(BlockedUserRecord entry)
    {
        Entry = entry;
        User = BlockedContentModelHelper.CreateBlockedUserPreview(Entry);
    }
}
