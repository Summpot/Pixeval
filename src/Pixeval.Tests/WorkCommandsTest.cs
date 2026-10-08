// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using Avalonia.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Native.Mako;
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
    public void IllustrationIsFavoriteRaisesPropertyChangedForIsFavoriteAndIsBookmarkedDisplay()
    {
        var illust = DesignHelper.DesignIllustration;
        var changedProperties = new List<string?>();
        illust.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        illust.IsFavorite = !illust.IsFavorite;

        CollectionAssert.Contains(changedProperties, nameof(Illustration.IsFavorite));
        CollectionAssert.Contains(changedProperties, nameof(Illustration.IsBookmarkedDisplay));
    }

    [TestMethod]
    public void NovelIsFavoriteRaisesPropertyChangedForIsFavoriteAndIsBookmarkedDisplay()
    {
        var novel = DesignHelper.DesignNovel;
        var changedProperties = new List<string?>();
        novel.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        novel.IsFavorite = !novel.IsFavorite;

        CollectionAssert.Contains(changedProperties, nameof(Novel.IsFavorite));
        CollectionAssert.Contains(changedProperties, nameof(Novel.IsBookmarkedDisplay));
    }
}
