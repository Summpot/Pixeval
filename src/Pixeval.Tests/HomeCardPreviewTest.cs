using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Common.Collections;
using Pixeval.Config;
using Pixeval.Mako;
using Pixeval.Localization;
using Pixeval.Pixiv;
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

    [TestMethod]
    public async Task InterruptedInitialPageRetriesTheSameEnumerator()
    {
        var engine = new InterruptedUsers();
        var model = new UserViewViewModel();
        model.ResetEngine(engine);
        using var preview = new HomeCardPreviewViewModel(new HomePageCardLayout(),
            _ => Task.FromResult(new HomeCardPreviewSource(model)));

        await preview.LoadAsync();

        Assert.HasCount(1, preview.Items);
        Assert.AreEqual(1, engine.EnumeratorCount);
        Assert.IsFalse(preview.CanRetry);
        Assert.IsNull(preview.PlaceholderText);
    }

    [TestMethod]
    public async Task FailedPageShowsFailureAndRetryCreatesAFreshSource()
    {
        var factories = 0;
        using var preview = new HomeCardPreviewViewModel(new HomePageCardLayout(), _ =>
        {
            var model = new UserViewViewModel();
            model.ResetEngine(++factories <= 2 ? Fail() : Single(new User { Id = 1 }));
            return Task.FromResult(new HomeCardPreviewSource(model));
        });

        await preview.LoadAsync();

        Assert.HasCount(0, preview.Items);
        Assert.IsTrue(preview.CanRetry);
        Assert.AreEqual(Strings.HomePageCardPreviewFailedTextBlockText(), preview.PlaceholderText);

        await preview.RetryCommand.ExecuteAsync(null);

        Assert.AreEqual(3, factories);
        Assert.HasCount(1, preview.Items);
        Assert.IsFalse(preview.CanRetry);
        Assert.IsNull(preview.PlaceholderText);
    }

    [TestMethod]
    public async Task EmptySuccessfulPageDoesNotShowFailure()
    {
        var model = new UserViewViewModel();
        using var preview = new HomeCardPreviewViewModel(new HomePageCardLayout(),
            _ => Task.FromResult(new HomeCardPreviewSource(model)));

        await preview.LoadAsync();

        Assert.IsFalse(preview.CanRetry);
        Assert.AreEqual(Strings.HomePageCardPreviewEmptyTextBlockText(), preview.PlaceholderText);
    }

    [TestMethod]
    public async Task ClosingCardCancelsPendingInitialRetry()
    {
        var engine = new InterruptedUsers();
        var model = new UserViewViewModel();
        model.ResetEngine(engine);
        var preview = new HomeCardPreviewViewModel(new HomePageCardLayout(),
            _ => Task.FromResult(new HomeCardPreviewSource(model)));

        var loading = preview.LoadAsync();
        preview.Dispose();
        await loading;

        Assert.AreEqual(1, engine.MoveNextCount);
        Assert.IsFalse(preview.CanRetry);
        Assert.IsNull(preview.ViewModel);
    }

    private static async IAsyncEnumerable<User> Fail()
    {
        await Task.Yield();
        yield return await Task.FromException<User>(new HttpRequestException("unavailable"));
    }

    private sealed class InterruptedUsers : IFetchEngine<User>
    {
        public IFetchEngineHandle EngineHandle { get; } = new DummyEngineHandle();
        public int EnumeratorCount { get; private set; }
        public int MoveNextCount { get; private set; }

        public IAsyncEnumerator<User> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            EnumeratorCount++;
            return new Enumerator(this);
        }

        private sealed class Enumerator(InterruptedUsers engine) : IAsyncEnumerator<User>
        {
            public User Current => new() { Id = 1 };
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            public ValueTask<bool> MoveNextAsync()
            {
                var count = ++engine.MoveNextCount;
                if (count > 2)
                    engine.EngineHandle.IsCompleted = true;
                return ValueTask.FromResult(count is 2);
            }
        }
    }

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
        var list = (IList)items;
        Assert.IsTrue(list.IsReadOnly);
        Assert.AreEqual(item, list[0]);

        object? collectionSender = null;
        object? countSender = null;
        ((INotifyCollectionChanged)items).CollectionChanged += (sender, _) => collectionSender = sender;
        ((INotifyPropertyChanged)items).PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(items.Count)) countSender = sender;
        };

        notifications = 0;
        model.Source.Clear();
        Assert.AreEqual(1, notifications);
        Assert.IsNotNull(preview.PlaceholderText);
        Assert.AreSame(items, collectionSender);
        Assert.AreSame(items, countSender);
    }

    private static async IAsyncEnumerable<T> Single<T>(T item)
    {
        await Task.Yield();
        yield return item;
    }
}
