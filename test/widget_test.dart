import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:pixeval_flutter/main.dart';
import 'package:pixeval_flutter/src/deeplink/deeplink_handler.dart';
import 'package:pixeval_flutter/src/ui/home_shell.dart';
import 'package:pixeval_flutter/src/ui/login_page.dart';
import 'package:pixeval_flutter/src/native/api/auth.dart';
import 'package:pixeval_flutter/src/native/api/booru.dart';
import 'package:pixeval_flutter/src/native/api/deeplink.dart';
import 'package:pixeval_flutter/src/native/api/download.dart';
import 'package:pixeval_flutter/src/native/api/events.dart';
import 'package:pixeval_flutter/src/native/api/feed.dart';
import 'package:pixeval_flutter/src/native/api/filters.dart';
import 'package:pixeval_flutter/src/native/api/image.dart';
import 'package:pixeval_flutter/src/native/api/network.dart';
import 'package:pixeval_flutter/src/native/api/novel.dart';
import 'package:pixeval_flutter/src/native/api/saucenao.dart';
import 'package:pixeval_flutter/src/native/api/search.dart';
import 'package:pixeval_flutter/src/native/api/storage.dart';
import 'package:pixeval_flutter/src/native/api/ugoira.dart';
import 'package:pixeval_flutter/src/native/native_api.dart';
import 'package:shared_preferences/shared_preferences.dart';

// Mock API for headless tests
class MockPixevalApi extends PixevalApi {
  @override
  Future<String> asyncPing({required String message}) async =>
      'async pong mock: $message';

  @override
  Future<void> initApp() async {}

  @override
  Future<String> ping({required String message}) async => 'pong mock: $message';

  @override
  Future<String> version() async => '0.1.0-test';

  @override
  Future<UserProfile?> getCurrentUser() async => null;

  @override
  Future<String?> getRefreshToken() async => null;

  @override
  Future<String?> getStoredRefreshToken() async => null;

  @override
  Future<void> releaseSearchSession({required String sessionId}) async {}

  @override
  Future<PkceAuthParamsDto> createPkceAuthParams() async =>
      const PkceAuthParamsDto(
        codeVerifier: 'mock_verifier',
        authUrl: 'https://app-api.pixiv.net/web/v1/login',
      );

  @override
  Future<AuthResponse> loginByCode({
    required String code,
    required String codeVerifier,
  }) async => const AuthResponse(
    accessToken: 'mock_access',
    refreshToken: 'mock_refresh',
    user: UserProfile(
      id: '123456',
      name: 'Test Pixiv User',
      account: 'test_user',
      mailAddress: 'test@pixiv.net',
      isPremium: true,
      avatarUrl: '',
    ),
  );

  @override
  Future<AuthResponse> loginByRefreshToken({
    required String refreshToken,
  }) async => const AuthResponse(
    accessToken: 'mock_access',
    refreshToken: 'mock_refresh',
    user: UserProfile(
      id: '123456',
      name: 'Test Pixiv User',
      account: 'test_user',
      mailAddress: 'test@pixiv.net',
      isPremium: true,
      avatarUrl: '',
    ),
  );

  @override
  Future<void> logout() async {}

  @override
  Future<AuthResponse> refreshSession() async => const AuthResponse(
    accessToken: 'mock_access',
    refreshToken: 'mock_refresh',
  );

  @override
  Stream<SessionEvent> createSessionEventStream({String? streamId}) =>
      const Stream.empty();

  @override
  Future<FlutterNetworkConfig> getNetworkConfig() async => FlutterNetworkConfig(
    domainFrontingEnabled: true,
    splitDelayMs: BigInt.from(100),
    proxyUrl: null,
    mirrorHost: null,
    dohUrl: null,
    hostIpsJson: '{}',
  );

  @override
  Future<ConnectivityCheckResult> testConnectivity() async =>
      ConnectivityCheckResult(
        success: true,
        latencyMs: BigInt.from(150),
        message: 'Mock connection successful',
      );

  @override
  Future<void> updateNetworkConfig({
    required FlutterNetworkConfig config,
  }) async {}

  @override
  Future<FeedPageResult> getRecommendedFeed({required bool refresh}) async =>
      const FeedPageResult(items: [], exhausted: true);

  @override
  Future<FeedPageResult> getRankingFeed({
    required String mode,
    String? date,
    required bool refresh,
  }) async => const FeedPageResult(items: [], exhausted: true);

  @override
  Future<FeedPageResult> getFollowFeed({
    required String restrict,
    required bool refresh,
  }) async => const FeedPageResult(items: [], exhausted: true);

  @override
  Future<IllustSummary> getIllustDetail({required int illustId}) async =>
      const IllustSummary(
        id: 1,
        title: 'Test Illust',
        caption: 'Test Caption',
        userId: 100,
        userName: 'Test Artist',
        userAccount: 'test_artist',
        userAvatarUrl: '',
        previewUrl: '',
        originalUrl: '',
        width: 1000,
        height: 1000,
        pageCount: 1,
        isBookmarked: false,
        totalBookmarks: 100,
        totalViews: 500,
        tags: ['test', 'pixiv'],
        isR18: false,
        isUgoira: false,
        pageUrls: [],
        originalPageUrls: [],
      );

  @override
  Future<bool> toggleBookmark({
    required int illustId,
    required bool isBookmarked,
  }) async => !isBookmarked;

  @override
  Future<void> clearImageCache() async {}

  @override
  Future<Uint8List> fetchImage({required String url, String? referer}) async =>
      Uint8List(0);

  @override
  Future<CacheStatsDto> getCacheStats() async => CacheStatsDto(
    entryCount: 42,
    totalAllocatedBytes: BigInt.from(1024 * 1024 * 1024),
    totalUsedBytes: BigInt.from(12 * 1024 * 1024),
  );

  @override
  Future<Uint8List?> getCachedImage({required String url}) async => null;

  @override
  Future<void> initImageCache({
    required String cacheDir,
    BigInt? capacityBytes,
  }) async {}

  @override
  Future<NovelContentDto> getNovelContentRendered({
    required int novelId,
  }) async => NovelContentDto(
    id: novelId,
    pages: ['Test novel page 1', 'Test novel page 2'],
    totalPages: 2,
    chapters: [],
  );

  @override
  Future<NovelDetailDto> getNovelDetail({required int novelId}) async =>
      NovelDetailDto(
        id: novelId,
        title: 'Test Novel',
        caption: 'Test novel caption',
        coverUrl: '',
        userId: 10,
        userName: 'Test Novelist',
        userAccount: 'test_novelist',
        userAvatarUrl: '',
        pageCount: 2,
        textLength: 1000,
        isBookmarked: false,
        totalBookmarks: 50,
        tags: ['original', 'fantasy'],
      );

  @override
  Future<List<AutocompleteTagDto>> getSearchAutocomplete({
    required String word,
  }) async => [
    AutocompleteTagDto(name: word, translatedName: 'Translated $word'),
  ];

  @override
  Future<FeedPageResult> searchIllustrations({
    required String word,
    String? sort,
    String? searchTarget,
    required bool refresh,
    String? sessionId,
  }) async => const FeedPageResult(items: [], exhausted: true);

  @override
  Future<FilterAnalysisResultDto> analyzeFilterQuery({
    required String query,
    required int caretPos,
  }) async => const FilterAnalysisResultDto(
    isValid: true,
    completions: [],
    diagnostics: [],
  );

  @override
  Future<List<IllustSummary>> filterArtworksByQuery({
    required String query,
    required List<IllustSummary> artworks,
  }) async => artworks;

  @override
  Future<BooruSearchResultDto> searchBooru({
    required String platformName,
    required String tags,
    required int page,
  }) async => BooruSearchResultDto(
    platform: platformName,
    results: [],
    searchTags: tags,
    pageNumber: page,
  );

  @override
  Future<List<SauceNaoItemDto>> searchSaucenao({
    required List<int> imageBytes,
    String? apiKey,
  }) async => [];

  @override
  Future<UgoiraMetadataDto> getUgoiraMetadata({required int illustId}) async =>
      const UgoiraMetadataDto(zipUrl: '', frames: []);

  @override
  Future<UgoiraFramesDto> extractUgoiraFramesFromZipBytes({
    required List<int> zipBytes,
    required List<int> delays,
  }) async => UgoiraFramesDto(frameBytes: [], delays: Uint32List(0));

  @override
  Future<void> exportUgoira({
    required String zipPath,
    required String outputPath,
    required String formatName,
    required List<int> frameDelaysMs,
  }) async {}

  @override
  Future<void> addSearchHistory({
    required String keyword,
    String? translatedName,
  }) async {}

  @override
  Future<void> clearAllBrowseHistory() async {}

  @override
  Future<void> clearAllSearchHistory() async {}

  @override
  Future<bool> deleteSearchHistoryItem({required String keyword}) async => true;

  @override
  Future<List<BrowseHistoryItemDto>> getBrowseHistory({
    required int skip,
    required int take,
  }) async => [];

  @override
  Future<List<SearchHistoryItemDto>> getSearchHistory({int? limit}) async => [];

  @override
  Future<void> initStorage({required String dbPath}) async {}

  @override
  Future<void> recordBrowseHistory({
    required String workId,
    required String title,
    required String previewUrl,
    required String authorName,
    String? workType,
  }) async {}

  @override
  Future<bool> cancelDownload({required String destination}) async => true;

  @override
  Future<void> clearAllDownloads() async {}

  @override
  Stream<DownloadProgressEvent> createDownloadEventStream({String? streamId}) =>
      const Stream.empty();

  @override
  Future<void> cancelSessionEventStream({String? streamId}) async {}

  @override
  Future<void> cancelDownloadEventStream({String? streamId}) async {}

  @override
  Future<void> initDownloadManager({required int concurrency}) async {}

  @override
  Future<bool> pauseDownload({required String destination}) async => true;

  @override
  Future<bool> removeDownload({required String destination}) async => true;

  @override
  Future<bool> resumeDownload({required String destination}) async => true;

  @override
  Future<void> startDownload({
    required String url,
    required String destination,
    String? artworkId,
    required bool overwrite,
  }) async {}

  @override
  Future<ParsedPixivLink> parsePixivUrl({required String url}) async =>
      const ParsedPixivLink.artwork(id: 12345);
}

void main() {
  setUpAll(() {
    SharedPreferences.setMockInitialValues({});
    NativeApi.initMock(api: MockPixevalApi());
  });

  testWidgets('Unauthenticated startup presents full-screen LoginGuidePage', (
    WidgetTester tester,
  ) async {
    await tester.pumpWidget(const ProviderScope(child: PixevalApp()));
    await tester.pumpAndSettle();

    expect(find.byType(LoginGuidePage), findsOneWidget);
    expect(find.text('欢迎使用 Pixeval'), findsOneWidget);
    expect(find.text('登录您的 Pixiv 账号以同步收藏与关注'), findsOneWidget);
    expect(find.text('打开 Pixiv 网页登录'), findsOneWidget);
    expect(find.text('遇到登录问题？查看排错指南'), findsOneWidget);
    expect(find.text('OAuth 网页授权登录'), findsOneWidget);
    expect(find.text('Refresh Token 登录'), findsOneWidget);
    expect(find.text('暂不登录，以访客模式浏览'), findsOneWidget);

    // Tap Refresh Token tab to test tab switching
    await tester.tap(find.text('Refresh Token 登录'));
    await tester.pumpAndSettle();
    expect(find.text('使用 Token 登录'), findsOneWidget);

    // Switch back to OAuth tab
    await tester.tap(find.text('OAuth 网页授权登录'));
    await tester.pumpAndSettle();
    expect(find.text('打开 Pixiv 网页登录'), findsOneWidget);
  });

  testWidgets('HomeShell navigation bar tabs smoke test in guest mode', (
    WidgetTester tester,
  ) async {
    await tester.pumpWidget(
      const ProviderScope(child: PixevalApp(initialGuestMode: true)),
    );
    await tester.pumpAndSettle();

    // Verify initial Explore tab
    expect(find.text('Explore'), findsOneWidget);
    expect(find.text('Discovery'), findsAtLeastNWidgets(1));

    // Tap Rankings tab
    await tester.tap(find.text('Rankings').first);
    await tester.pumpAndSettle();
    expect(find.text('Rankings'), findsAtLeastNWidgets(1));
    expect(find.text('Daily'), findsOneWidget);

    // Tap Feeds tab
    await tester.tap(find.text('Feeds'));
    await tester.pumpAndSettle();
    expect(find.text('Follow Feeds'), findsAtLeastNWidgets(1));

    // Tap Profile tab
    await tester.tap(find.text('Profile'));
    await tester.pumpAndSettle();
    expect(find.text('Profile & Settings'), findsOneWidget);
    expect(find.text('Not Logged In'), findsOneWidget);
    expect(find.text('Download Manager'), findsOneWidget);
    expect(find.text('Browsing History'), findsOneWidget);
    await tester.drag(find.byType(Scrollable).first, const Offset(0, -300));
    await tester.pumpAndSettle();
    expect(find.text('Anti-Blocking & Network'), findsOneWidget);
    expect(find.text('Image Cache'), findsOneWidget);

    // Tap Download Manager to open DownloadManagerPage
    await tester.tap(find.text('Download Manager'));
    await tester.pumpAndSettle();
    expect(find.text('Download Manager'), findsAtLeastNWidgets(1));
    expect(find.textContaining('All'), findsOneWidget);
    expect(find.textContaining('Active'), findsOneWidget);
    expect(find.textContaining('Completed'), findsOneWidget);

    // Pop back to Profile
    await tester.pageBack();
    await tester.pumpAndSettle();

    // Tap Browsing History to open HistoryPage
    await tester.tap(find.text('Browsing History'));
    await tester.pumpAndSettle();
    expect(find.text('Browsing History'), findsAtLeastNWidgets(1));
    expect(find.text('No browsing history yet'), findsOneWidget);
  });

  testWidgets('DeeplinkHandler handles artwork link navigation', (
    WidgetTester tester,
  ) async {
    await tester.pumpWidget(
      const ProviderScope(child: PixevalApp(initialGuestMode: true)),
    );
    await tester.pumpAndSettle();

    final context = tester.element(find.byType(HomeShell));
    await DeeplinkHandler.handleUrl(
      context,
      'https://www.pixiv.net/artworks/12345',
    );
    await tester.pumpAndSettle();

    // Verify detail page is opened
    expect(find.text('Test Illust'), findsOneWidget);
    expect(find.text('Test Artist'), findsOneWidget);
  });
}
