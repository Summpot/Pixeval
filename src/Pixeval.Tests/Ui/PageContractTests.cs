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
    [DataRow("zh-Hans")]
    [DataRow("en-US")]
    [DataRow("fr-FR")]
    [DataRow("ru-RU")]
    public Task IllustrationReader(string culture) => UiTestSession.RunAsync(TestContext, IllustrationReaderValidation.RunAsync, culture);

    [TestMethod]
    [DataRow("zh-Hans")]
    [DataRow("en-US")]
    [DataRow("fr-FR")]
    [DataRow("ru-RU")]
    public Task IllustrationNavigation(string culture) => UiTestSession.RunAsync(TestContext, IllustrationNavigationValidation.RunAsync, culture);

    [TestMethod]
    [DataRow("zh-Hans")]
    [DataRow("en-US")]
    [DataRow("fr-FR")]
    [DataRow("ru-RU")]
    public Task IllustrationAutoPlay(string culture) => UiTestSession.RunAsync(TestContext, IllustrationAutoPlayValidation.RunAsync, culture);

    [TestMethod]
    [DataRow("zh-Hans")]
    [DataRow("en-US")]
    [DataRow("fr-FR")]
    [DataRow("ru-RU")]
    public Task IllustrationSave(string culture) => UiTestSession.RunAsync(TestContext, IllustrationSaveValidation.RunAsync, culture);

    [TestMethod]
    [DataRow("zh-Hans")]
    [DataRow("en-US")]
    [DataRow("fr-FR")]
    [DataRow("ru-RU")]
    public Task IllustrationTransform(string culture) => UiTestSession.RunAsync(TestContext, IllustrationTransformValidation.RunAsync, culture);

    [TestMethod]
    [DataRow("zh-Hans")]
    [DataRow("en-US")]
    [DataRow("fr-FR")]
    [DataRow("ru-RU")]
    public Task IllustrationOriginal(string culture) => UiTestSession.RunAsync(TestContext, IllustrationOriginalValidation.RunAsync, culture);

    [TestMethod]
    public Task IllustrationImageLoad() => UiTestSession.RunAsync(TestContext, IllustrationImageLoadValidation.RunAsync);

    [TestMethod]
    [DataRow("zh-Hans")]
    [DataRow("en-US")]
    [DataRow("fr-FR")]
    [DataRow("ru-RU")]
    public Task IllustrationCopy(string culture) => UiTestSession.RunAsync(TestContext, IllustrationCopyValidation.RunAsync, culture);

    [TestMethod]
    [DataRow("zh-Hans")]
    [DataRow("en-US")]
    [DataRow("fr-FR")]
    [DataRow("ru-RU")]
    public Task IllustrationInformation(string culture) => UiTestSession.RunAsync(TestContext, IllustrationInfoValidation.RunAsync, culture);

    [TestMethod]
    [DataRow("zh-Hans")]
    [DataRow("en-US")]
    [DataRow("fr-FR")]
    [DataRow("ru-RU")]
    public Task IllustrationAnimation(string culture) => UiTestSession.RunAsync(TestContext, IllustrationAnimationValidation.RunAsync, culture);

    [TestMethod]
    [DataRow("zh-Hans")]
    [DataRow("en-US")]
    [DataRow("fr-FR")]
    [DataRow("ru-RU")]
    public Task IllustrationWorkActions(string culture) => UiTestSession.RunAsync(TestContext, IllustrationWorkActionsValidation.RunAsync, culture);

    [TestMethod]
    [DataRow("frames.gif", false)]
    [DataRow("frames.gif", true)]
    [DataRow("frames.webp", false)]
    [DataRow("frames.webp", true)]
    public Task IllustrationFramePlayback(string file, bool continuous) => UiTestSession.RunAsync(TestContext,
        directory => IllustrationFramePlaybackValidation.RunAsync(directory, file, continuous));

    [TestMethod]
    public Task NovelReader() => UiTestSession.RunAsync(TestContext, NovelReaderValidation.RunAsync);

    [TestMethod]
    public Task NovelReadingSettings() => UiTestSession.RunAsync(TestContext, NovelReadingSettingsValidation.RunAsync);

    [TestMethod]
    public Task NovelMarkdown() => UiTestSession.RunAsync(TestContext, NovelMarkdownValidation.RunAsync);

    [TestMethod]
    [DataRow("zh-Hans")]
    [DataRow("en-US")]
    [DataRow("fr-FR")]
    [DataRow("ru-RU")]
    public Task NovelActions(string culture) => UiTestSession.RunAsync(TestContext, NovelActionsValidation.RunAsync, culture);

    [TestMethod]
    [DataRow("zh-Hans")]
    [DataRow("en-US")]
    [DataRow("fr-FR")]
    [DataRow("ru-RU")]
    public Task NovelBookmarkLifetime(string culture) => UiTestSession.RunAsync(TestContext, NovelBookmarkLifetimeValidation.RunAsync, culture);

    [TestMethod]
    public Task NovelNativeExport() => UiTestSession.RunAsync(TestContext, NovelActionsValidation.RunNativeAsync, nativeExtension: true);

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public Task NovelExportCancellation(bool lateFailure, bool existingOutput) => UiTestSession.RunAsync(TestContext,
        directory => ExportCancellationValidation.RunAsync(directory, lateFailure, existingOutput: existingOutput));

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public Task UgoiraExportCancellation(bool lateFailure, bool existingOutput) => UiTestSession.RunAsync(TestContext,
        directory => ExportCancellationValidation.RunAsync(directory, lateFailure, animated: true, existingOutput: existingOutput));

    [TestMethod]
    [DataRow("zh-Hans")]
    [DataRow("en-US")]
    [DataRow("fr-FR")]
    [DataRow("ru-RU")]
    public Task NovelTranslation(string culture) => UiTestSession.RunAsync(TestContext, NovelTranslationValidation.RunAsync, culture);

    [TestMethod]
    public Task Documentation() => UiTestSession.RunAsync(TestContext, DocumentationValidation.RunAsync);

    [TestMethod]
    public Task Dialog() => UiTestSession.RunAsync(TestContext, DialogValidation.RunAsync);

    [TestMethod]
    public Task NavigationInRussian() => UiTestSession.RunAsync(TestContext, NavigationEditorValidation.RunAsync, culture: "ru-RU");
}
