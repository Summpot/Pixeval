// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;
using Pixeval.Native.Booru;
using Pixeval.Native.Mako;
using Pixeval.Native.SauceNao;
using Pixeval.Services;
using Pixeval.Utilities;
using Pixeval.ViewModels.Viewers;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ArtworkUiStateStoreTest
{
    [TestInitialize]
    public void Setup()
    {
        ArtworkUiStateStore.Clear();
        UserUiStateStore.Clear();
    }

    [TestMethod]
    public void IllustrationHydratesBookmarkStateCorrectly()
    {
        var illust = DesignHelper.DesignIllustration;
        var state = ArtworkUiStateStore.GetOrCreate(illust);

        Assert.AreEqual(illust.IsBookmarked ? HeartButtonState.Checked : HeartButtonState.Unchecked, state.BookmarkState);
        Assert.AreEqual(ArtworkUiStateStore.GetKey(illust), ArtworkUiStateStore.GetKey(illust));
    }

    [TestMethod]
    public void NovelHydratesBookmarkStateCorrectly()
    {
        var novel = DesignHelper.DesignNovel;
        var state = ArtworkUiStateStore.GetOrCreate(novel);

        Assert.AreEqual(novel.IsBookmarked ? HeartButtonState.Checked : HeartButtonState.Unchecked, state.BookmarkState);
    }

    [TestMethod]
    public void WorkEntryUnwrapsPolymorphicStateAndMatchesKey()
    {
        var illust = DesignHelper.DesignIllustration;
        var workEntry = new WorkEntry.Illust(illust);

        var keyFromIllust = ArtworkUiStateStore.GetKey(illust);
        var keyFromWorkEntry = ArtworkUiStateStore.GetKey(workEntry);
        Assert.AreEqual(keyFromIllust, keyFromWorkEntry);

        var state = ArtworkUiStateStore.GetOrCreate(workEntry);
        Assert.AreEqual(illust.IsBookmarked ? HeartButtonState.Checked : HeartButtonState.Unchecked, state.BookmarkState);

        // State instance should be shared across the same underlying work
        var stateDirect = ArtworkUiStateStore.GetOrCreate(illust);
        Assert.AreSame(state, stateDirect);
    }

    [TestMethod]
    public void StateStorePendingAndRevertFlow()
    {
        var illust = DesignHelper.DesignIllustration;
        var state = ArtworkUiStateStore.GetOrCreate(illust);

        ArtworkUiStateStore.SetBookmarkPending(illust);
        Assert.IsTrue((state.BookmarkState & HeartButtonState.Pending) is not 0);

        ArtworkUiStateStore.RevertBookmarkPending(illust, fallback: true);
        Assert.AreEqual(HeartButtonState.Checked, state.BookmarkState);

        ArtworkUiStateStore.SetWatchLater(illust, true);
        Assert.IsTrue(state.IsInWatchLater);

        ArtworkUiStateStore.SetWatchLater(illust, false);
        Assert.IsFalse(state.IsInWatchLater);
    }

    [TestMethod]
    public void UserViewerPageViewModelSynchronizesWithUserUiStateStore()
    {
        var user = DesignHelper.DesignUser;
        var response = DesignHelper.DesignSingleUserResponse;

        using var viewModel = new UserViewerPageViewModel(response);
        Assert.IsTrue(viewModel.IsFollowed);

        // Update from external store (e.g. simulated card interaction)
        UserUiStateStore.UpdateFollow(user, HeartButtonState.Unchecked);
        Assert.IsFalse(viewModel.IsFollowed);

        UserUiStateStore.UpdateFollow(user, HeartButtonState.Checked);
        Assert.IsTrue(viewModel.IsFollowed);
    }

    [TestMethod]
    public void NativeRecordsDoNotImplementINotifyPropertyChanged()
    {
        Assert.IsFalse(typeof(INotifyPropertyChanged).IsAssignableFrom(typeof(Illustration)));
        Assert.IsFalse(typeof(INotifyPropertyChanged).IsAssignableFrom(typeof(Novel)));
        Assert.IsFalse(typeof(INotifyPropertyChanged).IsAssignableFrom(typeof(BooruPost)));
        Assert.IsFalse(typeof(INotifyPropertyChanged).IsAssignableFrom(typeof(SauceNaoItem)));
        Assert.IsFalse(typeof(INotifyPropertyChanged).IsAssignableFrom(typeof(User)));
        Assert.IsFalse(typeof(INotifyPropertyChanged).IsAssignableFrom(typeof(WorkEntry)));
    }

    [TestMethod]
    public void SauceNaoItemIsFavoriteIsImmutable()
    {
        var item = new SauceNaoItem(99.5, "thumb", 0, "index", "title", "author", null, null, [], "pixiv", "123", false);
        Assert.IsFalse(item.IsFavorite);

        // Ensure property has getter and no setter via reflection
        var prop = typeof(SauceNaoItem).GetProperty(nameof(SauceNaoItem.IsFavorite));
        Assert.IsNotNull(prop);
        Assert.IsTrue(prop.CanRead);
        Assert.IsFalse(prop.CanWrite);
    }

    [TestMethod]
    public void WorkEntryIsFavoriteIsImmutable()
    {
        var illust = DesignHelper.DesignIllustration;
        var workEntry = new WorkEntry.Illust(illust);
        Assert.AreEqual(illust.IsFavorite, workEntry.IsFavorite);

        var prop = typeof(WorkEntry).GetProperty(nameof(WorkEntry.IsFavorite));
        Assert.IsNotNull(prop);
        Assert.IsTrue(prop.CanRead);
        Assert.IsFalse(prop.CanWrite);
    }
}
