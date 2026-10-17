using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Common;
using Pixeval.Config;
using Pixeval.Localization;
using Pixeval.Pixiv;
using Pixeval.Storage;
using Pixeval.Services.Core;
using Pixeval.ViewModels.Works;
using Pixeval.Views.Works;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WorkViewOwnershipTest
{
    [TestMethod]
    public async Task SourceInitializationWaitsForInheritedServices()
    {
        await RunAsync(() =>
        {
            using var view = new WorkView();
            view.ResetSource(AsyncEnumerable.Empty<object>(), SimpleWorkType.Illustration);
            Assert.IsNull(view.DataContext);

            var parent = new Border { Child = view };
            UiServices.SetProvider(parent, App.Services);
            Assert.IsInstanceOfType<IllustrationViewViewModel>(view.DataContext);
        });
    }

    [TestMethod]
    public async Task ExplicitModelReplacesPendingSource()
    {
        await RunAsync(() =>
        {
            using var view = new WorkView();
            view.ResetSource(AsyncEnumerable.Empty<object>(), SimpleWorkType.Illustration);
            var model = new IllustrationViewViewModel(new AppSettings());
            view.SetViewModel(model);
            var parent = new Border { Child = view };
            UiServices.SetProvider(parent, App.Services);
            Assert.AreSame(model, view.DataContext);
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task InitializingSourceDoesNotDisposeInheritedDataContext(bool incremental)
    {
        await RunAsync(() =>
        {
            using var parentModel = new DisposableContext();
            using var view = new WorkView();
            var parent = new Border { DataContext = parentModel, Child = view };
            UiServices.SetProvider(parent, App.Services);
            Assert.AreSame(parentModel, view.DataContext);

            if (incremental)
                view.ResetSource(AsyncEnumerable.Empty<object>(), SimpleWorkType.Illustration);
            else
                view.SetSource([], SimpleWorkType.Illustration);

            Assert.AreEqual(0, parentModel.DisposeCount);
            view.Dispose();
            Assert.AreEqual(0, parentModel.DisposeCount);
            GC.KeepAlive(parent);
        });
    }

    [TestMethod]
    public async Task OwnedModelsAreDisposedOnlyWhenReplacedOrClosed()
    {
        await RunAsync(() =>
        {
            using var view = new WorkView();
            var settings = new AppSettings();
            var first = new IllustrationViewViewModel(settings);
            view.SetViewModel(first);
            view.SetViewModel(first);
            first.ResetSource(AsyncEnumerable.Empty<object>());

            var second = new IllustrationViewViewModel(settings);
            view.SetViewModel(second);
            Assert.ThrowsExactly<ObjectDisposedException>(() => first.ResetSource(AsyncEnumerable.Empty<object>()));
            second.ResetSource(AsyncEnumerable.Empty<object>());

            view.Dispose();
            view.Dispose();
            Assert.ThrowsExactly<ObjectDisposedException>(() => second.ResetSource(AsyncEnumerable.Empty<object>()));
        });
    }

    [TestMethod]
    public async Task DisposingViewDoesNotDisposeInheritedDataContext()
    {
        await RunAsync(() =>
        {
            using var parentModel = new DisposableContext();
            using var view = new WorkView();
            var parent = new Border { DataContext = parentModel, Child = view };
            Assert.AreSame(parentModel, view.DataContext);

            view.Dispose();

            Assert.AreEqual(0, parentModel.DisposeCount);
            GC.KeepAlive(parent);
        });
    }

    private static async Task RunAsync(Action test)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerLoadingTest.ViewerApplication));
        await session.Dispatch(() =>
        {
            using var storage = new StorageEngine(":memory:");
            using var services = new ServiceCollection()
                .AddSingleton(new AppSettings()).AddSingleton(storage).BuildServiceProvider();
            var previous = App.Services;
            App.Services = services;
            try
            {
                test();
            }
            finally
            {
                App.Services = previous;
            }
        }, CancellationToken.None);
    }

    private sealed class DisposableContext : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }
}
