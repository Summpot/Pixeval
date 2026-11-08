using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Pixeval.Tests.Ui;

[TestClass]
[DoNotParallelize]
[TestCategory("UI")]
public sealed class PageContractTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public Task NavigationEditor() => UiTestSession.RunAsync(TestContext, NavigationEditorValidation.RunAsync);

    [TestMethod]
    public Task NativeExtension() => UiTestSession.RunAsync(TestContext, NativeExtensionValidation.RunAsync, nativeExtension: true);

    [TestMethod]
    public Task SearchCompletion() => UiTestSession.RunAsync(TestContext, SearchCompletionValidation.RunAsync);

    [TestMethod]
    public Task SearchArguments() => UiTestSession.RunAsync(TestContext, SearchArgumentsValidation.RunAsync);

    [TestMethod]
    public Task SearchPage() => UiTestSession.RunAsync(TestContext, SearchPageValidation.RunAsync);

    [TestMethod]
    public Task SauceNao() => UiTestSession.RunAsync(TestContext, SauceNaoValidation.RunAsync);

    [TestMethod]
    public Task UserPages() => UiTestSession.RunAsync(TestContext, UserPagesValidation.RunAsync);

    [TestMethod]
    public Task UserDetails() => UiTestSession.RunAsync(TestContext, UserDetailsValidation.RunAsync);

    [TestMethod]
    public Task SeriesPage() => UiTestSession.RunAsync(TestContext, SeriesPageValidation.RunAsync);

    [TestMethod]
    public Task SeriesDetails() => UiTestSession.RunAsync(TestContext, SeriesDetailsValidation.RunAsync);

    [TestMethod]
    public Task WorkSeries() => UiTestSession.RunAsync(TestContext, WorkSeriesValidation.RunAsync);

    [TestMethod]
    public Task WorkInfo() => UiTestSession.RunAsync(TestContext, WorkInfoValidation.RunAsync);

    [TestMethod]
    public Task DownloadFolders() => UiTestSession.RunAsync(TestContext, DownloadFoldersValidation.RunAsync);

    [TestMethod]
    public Task SubscriptionEditor() => UiTestSession.RunAsync(TestContext, SubscriptionEditorValidation.RunAsync);

    [TestMethod]
    public Task DownloadPages() => UiTestSession.RunAsync(TestContext, DownloadPagesValidation.RunAsync);

    [TestMethod]
    public Task CommentList() => UiTestSession.RunAsync(TestContext, CommentListValidation.RunAsync);

    [TestMethod]
    public Task CommentSend() => UiTestSession.RunAsync(TestContext, CommentSendValidation.RunAsync);

    [TestMethod]
    public Task WorkFeeds() => UiTestSession.RunAsync(TestContext, WorkFeedsValidation.RunAsync);

    [TestMethod]
    public Task WorkSources() => UiTestSession.RunAsync(TestContext, WorkSourcesValidation.RunAsync);

    [TestMethod]
    public Task WorkRelated() => UiTestSession.RunAsync(TestContext, WorkRelatedValidation.RunAsync);

    [TestMethod]
    public Task HistoryPages() => UiTestSession.RunAsync(TestContext, HistoryPagesValidation.RunAsync);

    [TestMethod]
    public Task HomeCards() => UiTestSession.RunAsync(TestContext, HomeCardsValidation.RunAsync);

    [TestMethod]
    public Task HomeParameters() => UiTestSession.RunAsync(TestContext, HomeParametersValidation.RunAsync);

    [TestMethod]
    public Task HomeSources() => UiTestSession.RunAsync(TestContext, HomeSourcesValidation.RunAsync);

    [TestMethod]
    public Task Spotlight() => UiTestSession.RunAsync(TestContext, SpotlightValidation.RunAsync);

    [TestMethod]
    public Task LoginLifetime() => UiTestSession.RunAsync(TestContext, LoginLifetimeValidation.RunAsync);

    [TestMethod]
    public Task WorkCards() => UiTestSession.RunAsync(TestContext, WorkCardsValidation.RunAsync);

    [TestMethod]
    public Task WorkSelection() => UiTestSession.RunAsync(TestContext, WorkSelectionValidation.RunAsync);

    [TestMethod]
    public Task ExtensionManagement() => UiTestSession.RunAsync(TestContext, ExtensionManagementValidation.RunAsync);

    [TestMethod]
    public Task BookmarkTags() => UiTestSession.RunAsync(TestContext, BookmarkTagsValidation.RunAsync);

    [TestMethod]
    public Task BookmarksPage() => UiTestSession.RunAsync(TestContext, BookmarksPageValidation.RunAsync);

    [TestMethod]
    public Task RankingPage() => UiTestSession.RunAsync(TestContext, RankingPageValidation.RunAsync);

    [TestMethod]
    public Task ViewerToolbar() => UiTestSession.RunAsync(TestContext, ViewerToolbarValidation.RunAsync);

    [TestMethod]
    public Task Documentation() => UiTestSession.RunAsync(TestContext, DocumentationValidation.RunAsync);

    [TestMethod]
    public Task Dialog() => UiTestSession.RunAsync(TestContext, DialogValidation.RunAsync);

    [TestMethod]
    public Task NavigationInRussian() => UiTestSession.RunAsync(TestContext, NavigationEditorValidation.RunAsync, culture: "ru-RU");
}
