// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using Avalonia.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using Pixeval.Services;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WorkCommandsTest
{
    [TestMethod]
    public void BookmarkRequestStructPropertiesHoldExpectedValues()
    {
        var illust = DesignHelper.DesignIllustration;
        var tags = new List<string> { "tag1", "tag2" };
        var request = new BookmarkRequest(illust, true, tags);

        Assert.AreSame(illust, request.Work);
        Assert.IsTrue(request.IsPrivate);
        Assert.AreSame(tags, request.Tags);
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
    public void AddToBookmarkCommandAcceptsBookmarkRequest()
    {
        var illust = DesignHelper.DesignIllustration;
        var request = new BookmarkRequest(illust, false, ["pixiv"]);
        Assert.IsTrue(WorkCommands.AddToBookmarkCommand.CanExecute(request));
    }

    [TestMethod]
    public void AddToWatchLaterCommandAcceptsWorkDirectly()
    {
        var illust = DesignHelper.DesignIllustration;
        Assert.IsTrue(WorkCommands.AddToWatchLaterCommand.CanExecute(illust));

        var novel = DesignHelper.DesignNovel;
        Assert.IsTrue(WorkCommands.AddToWatchLaterCommand.CanExecute(novel));
    }

    [TestMethod]
    public void SaveCommandAcceptsWorkDirectly()
    {
        var illust = DesignHelper.DesignIllustration;
        Assert.IsTrue(WorkCommands.SaveCommand.CanExecute(illust));

        var novel = DesignHelper.DesignNovel;
        Assert.IsTrue(WorkCommands.SaveCommand.CanExecute(novel));
    }

    [TestMethod]
    public void UserCommandsAcceptUserDirectly()
    {
        var user = DesignHelper.DesignUser;
        Assert.IsTrue(WorkCommands.FollowUserCommand.CanExecute(user));
        Assert.IsTrue(WorkCommands.BlockUserCommand.CanExecute(user));
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

    [TestMethod]
    public void ButtonWithAddToBookmarkCommandAcceptsBookmarkRequestParameter()
    {
        var illust = DesignHelper.DesignIllustration;
        var request = new BookmarkRequest(illust, true, null);
        var button = new Button
        {
            Command = WorkCommands.AddToBookmarkCommand,
            CommandParameter = request
        };
        Assert.IsTrue(button.Command.CanExecute(button.CommandParameter));
    }
}
