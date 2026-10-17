using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Config;
using Pixeval.Mako;
using Pixeval.Services.Background;
using Pixeval.Services.Core;
using Pixeval.Services.Network;
using Pixeval.Storage;
using Pixeval.ViewModels.Works;
using Pixeval.Views.Works;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WorkPageInitializationTest
{
    [TestMethod]
    [DataRow(typeof(WorkRecommendedPage))]
    [DataRow(typeof(WorkNewPage))]
    [DataRow(typeof(WorkRankingPage))]
    [DataRow(typeof(WorkFollowingPage))]
    [DataRow(typeof(WorkMyPixivPage))]
    public async Task NavigationCreatesPageBeforeUiServicesAreInherited(Type pageType)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerLoadingTest.ViewerApplication));
        await session.Dispatch(() =>
        {
            using var client = new MakoClient(ProxyHelper.CreateMakoConfiguration(new PixivDomainFrontingSettings()));
            using var storage = new StorageEngine(":memory:");
            using var services = new ServiceCollection()
                .AddSingleton(new AppSettings())
                .AddSingleton(client)
                .AddSingleton(storage)
                .AddSingleton(DispatchProxy.Create<IWorkSubscriptionService, UnusedSubscriptions>())
                .BuildServiceProvider();
            var previous = App.Services;
            App.Services = services; // XAML ServiceExtension uses the application provider.
            try
            {
                var navigation = new NavigationService(services);
                var page = navigation.CreatePage(pageType);
                using var view = page.GetLogicalDescendants().OfType<WorkView>().Single();
                Assert.IsNull(view.DataContext);

                var parent = new Border { Child = page };
                UiServices.SetProvider(parent, services);

                Assert.IsInstanceOfType<IllustrationViewViewModel>(view.DataContext);
                GC.KeepAlive(parent);
            }
            finally
            {
                App.Services = previous;
            }
        }, CancellationToken.None);
    }

    // These pages only require the service to be present; opening them must not start subscription work.
    public class UnusedSubscriptions : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new InvalidOperationException($"Unexpected subscription call: {targetMethod?.Name}");
    }
}
