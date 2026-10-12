// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using Avalonia.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;
using Pixeval.Pixiv;
using Pixeval.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Common;
using Pixeval.Services.Core;
using Pixeval.ViewModels.Works;
using Pixeval.Services.Media;

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
    public void WorkCommands_AcceptsWorkEntryDirectly()
    {
        var illust = DesignHelper.DesignIllustration;
        WorkEntry workEntry = new WorkEntry(new WorkEntry.Illust(illust));

        Assert.IsTrue(WorkCommands.BookmarkCommand.CanExecute(workEntry));
        Assert.IsTrue(WorkCommands.AddToWatchLaterCommand.CanExecute(workEntry));
        Assert.IsTrue(WorkCommands.SaveCommand.CanExecute(workEntry));

        var request = new BookmarkRequest(workEntry, false, ["tag"]);
        Assert.IsTrue(WorkCommands.AddToBookmarkCommand.CanExecute(request));
    }

    [TestMethod]
    public void StorageRecords_TryCreateWorkKey_SupportsWorkEntry()
    {
        var illust = DesignHelper.DesignIllustration;
        WorkEntry workEntry = new WorkEntry(new WorkEntry.Illust(illust));
        var variant = new WorkEntry.Illust(illust);

        Assert.IsTrue(Pixeval.Storage.WatchLaterRecord.TryCreateWorkKey(illust, out var key1));
        Assert.IsTrue(Pixeval.Storage.WatchLaterRecord.TryCreateWorkKey(workEntry, out var key2));
        Assert.IsTrue(Pixeval.Storage.WatchLaterRecord.TryCreateWorkKey(variant, out var key3));
        Assert.AreEqual(key1, key2);
        Assert.AreEqual(key1, key3);

        Assert.IsTrue(Pixeval.Storage.BrowseHistoryRecord.TryCreateWorkKey(illust, out var hkey1));
        Assert.IsTrue(Pixeval.Storage.BrowseHistoryRecord.TryCreateWorkKey(workEntry, out var hkey2));
        Assert.IsTrue(Pixeval.Storage.BrowseHistoryRecord.TryCreateWorkKey(variant, out var hkey3));
        Assert.AreEqual(hkey1, hkey2);
        Assert.AreEqual(hkey1, hkey3);
    }
}
