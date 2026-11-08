using System;
using System.Linq;
using System.Net.Http;
using Pixeval.ViewModels;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Utilities;
using Pixeval.Views.Login;
using Pixeval.Views.Search;
using Pixeval.Views.Shell;

namespace Pixeval.Tests.Ui;

[TestClass]
[DoNotParallelize]
[TestCategory("UI")]
public sealed class ProductionShellTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public Task StartupReplacementAndWindowCloseUseTheSameLifetime() => UiTestSession.RunAsync(TestContext, async _ =>
    {
        var shell = App.CreateViewContainer();
        Assert.AreEqual(typeof(TabViewContainer), shell.GetType());
        var window = new Window { Width = 1000, Height = 720, Content = shell };
        var first = new LifetimePage();
        var replacement = new LifetimePage();
        try
        {
            await App.OpenInitialPageAsync(shell);
            Pixeval.Common.MaterialAnimation.Initialize(window);
            window.Show();
            Render(window);
            Assert.IsInstanceOfType<LoginPage>(shell.SelectedPage);
            shell.NavigateTo(first, removeCurrentPage: true);
            Render(window);
            Assert.AreEqual(1, shell.Pages.Count);
            shell.NavigateTo(replacement, removeCurrentPage: true);
            Render(window);
            Assert.AreEqual(1, first.DisposeCount, "Replacement must use the normal close route.");
            Assert.AreSame(replacement, shell.SelectedPage);
            window.Close();
            Assert.AreEqual(1, replacement.DisposeCount, "Closing the original window must release its page, just like a torn-off window.");
            Assert.AreEqual(0, shell.Pages.Count);
            shell.CloseAllPages();
            Assert.AreEqual(1, replacement.DisposeCount);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public Task SearchNavigatesToRealResultsAndClosingCancelsPendingCompletion() => UiTestSession.RunAsync(TestContext, async _ =>
    {
        var client = App.AppViewModel.MakoClient;
        using var responses = new OfflineSearchFixture(client);
        client.SetRefreshToken("fixture-shell");
        await FixtureAuthentication.IdentifyAsync(client);
        var shell = App.CreateViewContainer();
        var page = UiTestSession.Create<SearchPage>();
        var window = new Window { Width = 1000, Height = 720, Content = shell };
        try
        {
            shell.NavigateTo(page);
            Pixeval.Common.MaterialAnimation.Initialize(window);
            window.Show();
            Render(window);
            var box = page.FindControl<AutoCompleteBox>("SearchAutoCompleteBox")!;
            var editor = box.GetVisualDescendants().OfType<TextBox>().First();
            Assert.IsTrue(editor.Focus());
            window.KeyTextInput("shell-query");
            var pending = await responses.NextCompletionAsync("shell-query");
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            Render(window);
            Assert.IsInstanceOfType<WorkSearchResultPage>(shell.SelectedPage);
            Assert.AreEqual(2, shell.Pages.Count);
            Assert.IsTrue(pending.Token.IsCancellationRequested, "Real tab switching unloads the search page and cancels completion.");
            shell.ClosePage(shell.SelectedPage!);
            shell.SelectPage(page);
            Render(window);
            var resumed = await responses.NextCompletionAsync("shell-query");
            shell.ClosePage(page);
            Render(window);
            Assert.IsTrue(resumed.Token.IsCancellationRequested);
            pending.Complete("obsolete");
            resumed.Complete("closed");
            await Task.Delay(50);
            Assert.AreEqual(0, shell.Pages.Count);
            Assert.IsFalse(box.ItemsSource?.Cast<SearchCompletionItem>().Any(item => item.Text is "obsolete" or "closed") ?? false);
            Assert.IsTrue(responses.UnexpectedRequests.IsEmpty);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public Task ShutdownDrainsHistoryRestoreBeforeDisposingDatabase() => UiTestSession.RunAsync(TestContext, async _ =>
    {
        var history = App.AppViewModel;
        await App.AppViewModel.App.DisposeAsync();
        Assert.IsTrue(history.RestoreTask.IsCompletedSuccessfully);
    });

    [TestMethod]
    public Task LateLoginFailureAfterShutdownDoesNotResolveDisposedServices() => UiTestSession.RunAsync(TestContext, async _ =>
    {
        using var responses = new OfflineSearchFixture(App.AppViewModel.MakoClient);
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        responses.AuthResponseFactory = (_, _) =>
        {
            requested.TrySetResult();
            return response.Task; // Deliberately ignore cancellation until after shutdown.
        };
        var shell = App.CreateViewContainer();
        var page = UiTestSession.Create<LoginPage>();
        var window = new Window { Width = 1000, Height = 720, Content = shell };
        try
        {
            shell.NavigateTo(page);
            Pixeval.Common.MaterialAnimation.Initialize(window);
            window.Show();
            Render(window);
            var model = (LoginPageViewModel) page.DataContext!;
            await model.LoadUsersAsync();
            model.RefreshToken = "fixture-shutdown";
            var button = page.GetVisualDescendants().OfType<Button>().Single(control =>
                Equals(control.Content, Strings.LoginPageRefreshTokenButtonContent()));
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await requested.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var completion = page.LoginCompletion;
            window.Close();
            await App.AppViewModel.App.DisposeAsync();
            response.TrySetException(new HttpRequestException("Late failure after application shutdown"));
            await completion.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(model.IsLoginInProgress);
            Assert.AreEqual(0, shell.Pages.Count);
        }
        finally
        {
            window.Close();
            response.TrySetCanceled();
            await page.LoginCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        }
    });

    [TestMethod]
    public async Task HyperlinksRenderAcrossSequentialApplications()
    {
        for (var iteration = 0; iteration < 2; iteration++)
            await UiTestSession.RunAsync(TestContext, _ =>
            {
                var shell = App.CreateViewContainer();
                shell.NavigateTo(new ContentPage
                {
                    Content = new StackPanel
                    {
                        Children =
                        {
                            new HyperlinkButton { Content = "Default link" },
                            new HyperlinkButton
                            {
                                Content = "Inline link",
                                Theme = (ControlTheme)Application.Current!.FindResource("InlineHyperlinkTheme")!
                            }
                        }
                    }
                });
                var window = new Window { Width = 800, Height = 600, Content = shell };
                try
                {
                    Pixeval.Common.MaterialAnimation.Initialize(window);
                    window.Show();
                    Render(window);
                    using var frame = window.CaptureRenderedFrame();
                    Assert.IsNotNull(frame, "A new application must render links without retaining the previous UI dispatcher.");
                }
                finally { window.Close(); }
                return Task.CompletedTask;
            });
    }

    [TestMethod]
    public Task ComposedDriverLeavesDestinationOpenUntilExplicitClose() => UiTestSession.RunAsync(TestContext, _ =>
    {
        var origin = new LifetimePage();
        var destination = new LifetimePage();
        Page? observed = null;
        var driver = new PageTestHost { Content = origin, NavigationObserver = page => observed = page };
        var window = new Window { Width = 800, Height = 600, Content = driver.Shell };
        try
        {
            Pixeval.Common.MaterialAnimation.Initialize(window);
            window.Show();
            Render(window);
            driver.Shell.NavigateTo(destination);
            Render(window);
            Assert.AreSame(driver.Shell, window.Content);
            Assert.AreEqual(typeof(TabViewContainer), driver.Shell.GetType());
            Assert.AreSame(destination, observed);
            Assert.AreSame(destination, driver.Shell.SelectedPage);
            Assert.AreEqual(2, driver.Shell.Pages.Count);
            Assert.IsTrue(destination.IsLoaded);
            Assert.AreEqual(0, destination.DisposeCount, "Observing navigation must not close its destination.");
            driver.CloseDestinationAndReturn();
            Render(window);
            Assert.AreSame(origin, driver.Shell.SelectedPage);
            Assert.AreEqual(1, destination.DisposeCount);
            Assert.AreEqual(1, driver.Navigations);
            window.Close();
            Assert.AreEqual(1, origin.DisposeCount);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task ComposedDriverCannotRestoreAReplacedOrigin() => UiTestSession.RunAsync(TestContext, _ =>
    {
        var origin = new LifetimePage();
        var replacement = new LifetimePage();
        var driver = new PageTestHost { Content = origin };
        var window = new Window { Width = 800, Height = 600, Content = driver.Shell };
        try
        {
            Pixeval.Common.MaterialAnimation.Initialize(window);
            window.Show();
            Render(window);
            driver.Shell.NavigateTo(replacement, removeCurrentPage: true);
            Render(window);
            Assert.AreEqual(1, origin.DisposeCount);
            Assert.AreSame(replacement, driver.Shell.SelectedPage);
            Assert.AreEqual(1, driver.Shell.Pages.Count);
            Assert.Throws<InvalidOperationException>(() => driver.CloseDestinationAndReturn());
            Assert.AreSame(replacement, driver.Shell.SelectedPage);
            Assert.AreEqual(0, replacement.DisposeCount);
            window.Close();
            Assert.AreEqual(1, replacement.DisposeCount);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    });

    private static void Render(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private sealed class LifetimePage : ContentPage, IDisposable
    {
        internal int DisposeCount { get; private set; }
        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            RaiseEvent(new ViewModelDisposalEventArgs(ViewModelDisposal.ViewModelDisposalEvent, this));
        }
        public void Dispose() => DisposeCount++;
    }
}
