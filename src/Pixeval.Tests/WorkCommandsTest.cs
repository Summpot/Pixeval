// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using Avalonia.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;
using Pixeval.Native.Mako;
using Pixeval.Services;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;

namespace Pixeval.Tests;

[TestClass]
public sealed class WorkCommandsTest
{
    [TestMethod]
    public void ResolveWorkDirectInstanceReturnsWork()
    {
        var illust = DesignHelper.DesignIllustration;
        var resolved = WorkCommands.ResolveWork(illust);
        Assert.AreSame(illust, resolved);
    }

    [TestMethod]
    public void ResolveWorkControlWithWorkDataContextReturnsWork()
    {
        var illust = DesignHelper.DesignIllustration;
        var control = new Button { DataContext = illust };
        var resolved = WorkCommands.ResolveWork(control);
        Assert.AreSame(illust, resolved);
    }

    [TestMethod]
    public void ResolveWorkControlWithIllustrationViewerPageViewModelReturnsCurrentIllustration()
    {
        var illust = DesignHelper.DesignIllustration;
        var viewerVm = new IllustrationViewerPageViewModel(illust, false);
        var button = new Button { DataContext = viewerVm };
        var resolved = WorkCommands.ResolveWork(button);
        Assert.AreSame(illust, resolved);
    }

    [TestMethod]
    public void ResolveWorkControlWithNovelViewerPageViewModelReturnsCurrentNovel()
    {
        var novel = DesignHelper.DesignNovel;
        var novelVm = new NovelViewerPageViewModel(novel, false);
        var button = new Button { DataContext = novelVm };
        var resolved = WorkCommands.ResolveWork(button);
        Assert.AreSame(novel, resolved);
    }

    [TestMethod]
    public void ArtworkUiStateStoreTracksBookmarkAndRaisesPropertyChanged()
    {
        ArtworkUiStateStore.Clear();
        var illust = DesignHelper.DesignIllustration;
        var state = ArtworkUiStateStore.GetOrCreate(illust);
        Assert.AreEqual(HeartButtonState.Checked, state.BookmarkState);

        var changedProperties = new List<string?>();
        state.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        ArtworkUiStateStore.UpdateBookmark(illust, HeartButtonState.Unchecked);

        Assert.AreEqual(HeartButtonState.Unchecked, state.BookmarkState);
        CollectionAssert.Contains(changedProperties, nameof(ArtworkUiState.BookmarkState));
    }

    [TestMethod]
    public void UserUiStateStoreTracksFollowAndRaisesPropertyChanged()
    {
        UserUiStateStore.Clear();
        var user = DesignHelper.DesignUser;
        var state = UserUiStateStore.GetOrCreate(user);
        Assert.AreEqual(HeartButtonState.Checked, state.FollowState);

        var changedProperties = new List<string?>();
        state.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        UserUiStateStore.UpdateFollow(user, HeartButtonState.Unchecked);

        Assert.AreEqual(HeartButtonState.Unchecked, state.FollowState);
        CollectionAssert.Contains(changedProperties, nameof(UserUiState.FollowState));
    }

    [TestMethod]
    public void BookmarkCommandAcceptsWorkDirectlyWithoutThrowing()
    {
        var illust = DesignHelper.DesignIllustration;
        Assert.IsTrue(WorkCommands.BookmarkCommand.CanExecute(illust));

        var novel = DesignHelper.DesignNovel;
        Assert.IsTrue(WorkCommands.BookmarkCommand.CanExecute(novel));
    }

    [TestMethod]
    public void ButtonWithBookmarkCommandAcceptsWorkAsCommandParameter()
    {
        var illust = DesignHelper.DesignIllustration;
        var button = new Button
        {
            Command = WorkCommands.BookmarkCommand,
            CommandParameter = illust
        };
        Assert.IsTrue(button.Command.CanExecute(button.CommandParameter));
    }
}
