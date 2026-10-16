using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Common.Collections;
using Pixeval.Config;
using Pixeval.Mako;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Home;
using Pixeval.ViewModels.Works;

namespace Pixeval.Tests;

[TestClass]
public sealed class HomeCardPreviewTest
{
    [TestMethod]
    public Task RecommendedUsersLoadInitialPreview() => AssertPreviewLoadsAsync(
        new UserViewViewModel(),
        new User(123, "Author", "author", new ProfileImageUrls(null, null, null, null), false, null, []));

    [TestMethod]
    public Task SpotlightLoadsInitialPreview() => AssertPreviewLoadsAsync(new SpotlightViewViewModel(), default(SpotlightArticle));

    private static async Task AssertPreviewLoadsAsync<T>(EntryViewViewModel<T, T> model, T item) where T : notnull
    {
        model.ResetEngine(Single(item));
        using var preview = new HomeCardPreviewViewModel(new HomePageCardLayout(),
            _ => Task.FromResult(new HomeCardPreviewSource(model)));
        var notifications = 0;
        preview.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(preview.Items)) notifications++;
        };

        await preview.LoadAsync();

        Assert.HasCount(1, preview.Items);
        Assert.AreEqual(item, preview.Items.Single());
        Assert.IsNull(preview.PlaceholderText);
        Assert.IsGreaterThanOrEqualTo(2, notifications);
        var items = preview.Items;
        Assert.AreSame(items, preview.Items);
        Assert.IsInstanceOfType<IIncrementalLoading>(items);
        Assert.IsInstanceOfType<INotifyCollectionChanged>(items);

        notifications = 0;
        model.Source.Clear();
        Assert.AreEqual(1, notifications);
        Assert.IsNotNull(preview.PlaceholderText);
    }

    private static async IAsyncEnumerable<T> Single<T>(T item)
    {
        await Task.Yield();
        yield return item;
    }
}
