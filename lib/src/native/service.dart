import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:uniffi_dart/uniffi_dart.dart';

import 'api_contract.dart';
import 'pixeval.g.dart';
import 'models.dart';
import 'presentation.dart';

/// Application-level ownership of pagination and event subscriptions. All ABI
/// conversion and native lifecycle mechanics belong to uniffi_dart.
final class PixevalService extends PixevalApi {
  PixevalService(this.bindings);
  final PixevalNative bindings;
  final _feeds = <String, ({String signature, AppIllustrationFeed feed})>{};
  final _subscriptions = <String, _EventLease>{};
  var _nextSubscription = 0;

  Future<FeedPageResult> _page(
    String key,
    Object signature,
    bool refresh,
    AppIllustrationFeed Function() create,
  ) async {
    final encoded = jsonEncode(signature);
    var entry = _feeds[key];
    if (entry == null || refresh || entry.signature != encoded) {
      if (entry != null) {
        entry.feed.cancel();
        unawaited(entry.feed.dispose());
      }
      entry = (signature: encoded, feed: create());
      _feeds[key] = entry;
    }
    final page = await entry.feed.nextPage();
    var items = page.items.map(presentIllustration).toList();
    final filter = entry.feed.filterQuery();
    if (filter != null) {
      items = await filterArtworksByQuery(query: filter, artworks: items);
    }
    return FeedPageResult(items: items, exhausted: page.exhausted);
  }

  @override
  Future<FeedPageResult> getRecommendedFeed({required bool refresh}) => _page(
    'recommended',
    [],
    refresh,
    bindings.appIllustrationFeedRecommended,
  );
  @override
  Future<FeedPageResult> getRankingFeed({
    required String mode,
    String? date,
    required bool refresh,
  }) => _page(
    'ranking',
    [mode, date],
    refresh,
    () => bindings.appIllustrationFeedRanking(mode: mode, date: date),
  );
  @override
  Future<FeedPageResult> getFollowFeed({
    required String restrict,
    required bool refresh,
  }) => _page(
    'following',
    [restrict],
    refresh,
    () => bindings.appIllustrationFeedFollowing(restrict: restrict),
  );
  @override
  Future<FeedPageResult> searchIllustrations({
    required String word,
    String? sort,
    String? searchTarget,
    required bool refresh,
    String? sessionId,
  }) => _page(
    'search:${sessionId ?? 'legacy'}',
    [word, sort, searchTarget],
    refresh,
    () => bindings.appIllustrationFeedSearch(
      word: word,
      sort: sort,
      searchTarget: searchTarget,
    ),
  );
  @override
  Future<void> releaseSearchSession({required String sessionId}) async {
    final entry = _feeds.remove('search:$sessionId');
    if (entry != null) {
      entry.feed.cancel();
      await entry.feed.dispose();
    }
  }

  Future<void> _clearFeeds() async {
    final feeds = _feeds.values.toList();
    _feeds.clear();
    for (final entry in feeds) {
      entry.feed.cancel();
    }
    await Future.wait(feeds.map((entry) => entry.feed.dispose()));
  }

  Stream<T> _events<T, R>(
    String category,
    String? id,
    R Function() create,
    Future<T?> Function(R) next,
    void Function(R) cancel,
    Future<void> Function(R) release,
  ) async* {
    final key = '$category:${id ?? 'anonymous-${++_nextSubscription}'}';
    final previous = _subscriptions.remove(key);
    await previous?.close();
    final receiver = create();
    final lease = _EventLease(() => cancel(receiver), () => release(receiver));
    _subscriptions[key] = lease;
    yield* nativeEventStream<T>(
      next: () => next(receiver),
      cancel: lease.cancel,
      dispose: () async {
        if (identical(_subscriptions[key], lease)) _subscriptions.remove(key);
        await lease.close();
      },
    );
  }

  Future<void> _cancelEvents(String category, String? id) async {
    final keys = _subscriptions.keys
        .where(
          (key) => id == null
              ? key.startsWith('$category:')
              : key == '$category:$id',
        )
        .toList();
    for (final key in keys) {
      await _subscriptions.remove(key)?.close();
    }
  }

  @override
  Stream<SessionEvent> createSessionEventStream({String? streamId}) => _events(
    'session',
    streamId,
    bindings.sessionEventReceiverNew,
    (receiver) => receiver.next(),
    (receiver) => receiver.cancel(),
    (receiver) => receiver.dispose(),
  );
  @override
  Stream<DownloadProgressEvent> createDownloadEventStream({String? streamId}) =>
      _events(
        'download',
        streamId,
        bindings.downloadEventReceiverNew,
        (receiver) => receiver.next(),
        (receiver) => receiver.cancel(),
        (receiver) => receiver.dispose(),
      );
  @override
  Future<void> cancelSessionEventStream({String? streamId}) =>
      _cancelEvents('session', streamId);
  @override
  Future<void> cancelDownloadEventStream({String? streamId}) =>
      _cancelEvents('download', streamId);
  @override
  Future<void> close() async {
    for (final subscription in _subscriptions.values.toList()) {
      await subscription.close();
    }
    _subscriptions.clear();
    await _clearFeeds();
    await bindings.dispose();
  }

  @override
  Future<void> addSearchHistory({
    required String keyword,
    String? translatedName,
  }) async => bindings.addSearchHistory(
    keyword: keyword,
    translatedName: translatedName,
  );
  @override
  Future<FilterAnalysisResultDto> analyzeFilterQuery({
    required String query,
    required int caretPos,
  }) async => bindings.analyzeFilterQuery(query: query, caretPos: caretPos);
  @override
  Future<String> asyncPing({required String message}) async =>
      bindings.asyncPing(message: message);
  @override
  Future<bool> cancelDownload({required String destination}) async =>
      bindings.cancelDownload(destination: destination);
  @override
  Future<void> clearAllBrowseHistory() async =>
      bindings.clearAllBrowseHistory();
  @override
  Future<void> clearAllDownloads() async => bindings.clearAllDownloads();
  @override
  Future<void> clearAllSearchHistory() async =>
      bindings.clearAllSearchHistory();
  @override
  Future<void> clearImageCache() async => bindings.clearImageCache();
  @override
  Future<PkceAuthParamsDto> createPkceAuthParams() async =>
      bindings.createPkceAuthParams();
  @override
  Future<bool> deleteSearchHistoryItem({required String keyword}) async =>
      bindings.deleteSearchHistoryItem(keyword: keyword);
  @override
  Future<void> exportUgoira({
    required String zipPath,
    required String outputPath,
    required String formatName,
    required List<int> frameDelaysMs,
  }) async => bindings.exportUgoira(
    zipPath: zipPath,
    outputPath: outputPath,
    formatName: formatName,
    frameDelaysMs: frameDelaysMs,
  );
  @override
  Future<UgoiraFramesDto> extractUgoiraFramesFromZipBytes({
    required List<int> zipBytes,
    required List<int> delays,
  }) async => bindings.extractUgoiraFramesFromZipBytes(
    zipBytes: Uint8List.fromList(zipBytes),
    delays: delays,
  );
  @override
  Future<Uint8List> fetchImage({required String url, String? referer}) async =>
      bindings.fetchImage(url: url, referer: referer);
  @override
  Future<List<IllustSummary>> filterArtworksByQuery({
    required String query,
    required List<IllustSummary> artworks,
  }) async {
    final matches = await bindings.filterArtworksByQuery(
      query: query,
      artworks: artworks.map(filterMetadata).toList(),
    );
    if (matches.length != artworks.length) {
      throw StateError('Native filter returned an invalid result length');
    }
    return [
      for (var i = 0; i < artworks.length; i++)
        if (matches[i]) artworks[i],
    ];
  }

  @override
  Future<List<BrowseHistoryItemDto>> getBrowseHistory({
    required int skip,
    required int take,
  }) async => bindings.getBrowseHistory(skip: skip, take: take);
  @override
  Future<CacheStatsDto> getCacheStats() async => bindings.getCacheStats();
  @override
  Future<Uint8List?> getCachedImage({required String url}) async =>
      bindings.getCachedImage(url: url);
  @override
  Future<UserProfile?> getCurrentUser() async => bindings.getCurrentUser();
  @override
  Future<IllustSummary> getIllustDetail({required int illustId}) async =>
      presentIllustration(await bindings.getIllustDetail(illustId: illustId));
  @override
  Future<FlutterNetworkConfig> getNetworkConfig() async =>
      bindings.getNetworkConfig();
  @override
  Future<NovelContentDto> getNovelContentRendered({
    required int novelId,
  }) async => bindings.getNovelContentRendered(novelId: novelId);
  @override
  Future<NovelDetailDto> getNovelDetail({required int novelId}) async =>
      bindings.getNovelDetail(novelId: novelId);
  @override
  Future<String?> getRefreshToken() async => bindings.getRefreshToken();
  @override
  Future<List<AutocompleteTagDto>> getSearchAutocomplete({
    required String word,
  }) async => bindings.getSearchAutocomplete(word: word);
  @override
  Future<List<SearchHistoryItemDto>> getSearchHistory({
    required int limit,
  }) async => bindings.getSearchHistory(limit: limit);
  @override
  Future<String?> getStoredRefreshToken() async =>
      bindings.getStoredRefreshToken();
  @override
  Future<UgoiraMetadataDto> getUgoiraMetadata({required int illustId}) async =>
      bindings.getUgoiraMetadata(illustId: illustId);
  @override
  Future<void> initApp() async => bindings.initApp();
  @override
  Future<void> initDownloadManager({required int concurrency}) async =>
      bindings.initDownloadManager(concurrency: concurrency);
  @override
  Future<void> initImageCache({
    required String cacheDir,
    BigInt? capacityBytes,
  }) async =>
      bindings.initImageCache(cacheDir: cacheDir, capacityBytes: capacityBytes);
  @override
  Future<void> initStorage({required String dbPath}) async =>
      bindings.initStorage(dbPath: dbPath);
  @override
  Future<AuthResponse> loginByCode({
    required String code,
    required String codeVerifier,
  }) async {
    final response = await bindings.loginByCode(
      code: code,
      codeVerifier: codeVerifier,
    );
    await _clearFeeds();
    return response;
  }

  @override
  Future<AuthResponse> loginByRefreshToken({
    required String refreshToken,
  }) async {
    final response = await bindings.loginByRefreshToken(
      refreshToken: refreshToken,
    );
    await _clearFeeds();
    return response;
  }

  @override
  Future<void> logout() async {
    await _clearFeeds();
    await bindings.logout();
  }

  @override
  Future<ParsedPixivLink> parsePixivUrl({required String url}) async =>
      bindings.parsePixivUrl(url: url);
  @override
  Future<bool> pauseDownload({required String destination}) async =>
      bindings.pauseDownload(destination: destination);
  @override
  Future<String> ping({required String message}) async =>
      bindings.ping(message: message);
  @override
  Future<void> recordBrowseHistory({
    required String workId,
    required String title,
    required String previewUrl,
    required String authorName,
    String? workType,
  }) async => bindings.recordBrowseHistory(
    workId: workId,
    title: title,
    previewUrl: previewUrl,
    authorName: authorName,
    workType: workType,
  );
  @override
  Future<AuthResponse> refreshSession() async => bindings.refreshSession();
  @override
  Future<bool> removeDownload({required String destination}) async =>
      bindings.removeDownload(destination: destination);
  @override
  Future<bool> resumeDownload({required String destination}) async =>
      bindings.resumeDownload(destination: destination);
  @override
  Future<BooruSearchResultDto> searchBooru({
    required String platformName,
    required String tags,
    required int page,
  }) async =>
      bindings.searchBooru(platformName: platformName, tags: tags, page: page);
  @override
  Future<List<SauceNaoItemDto>> searchSaucenao({
    required List<int> imageBytes,
    String? apiKey,
  }) async => bindings.searchSaucenao(
    imageBytes: Uint8List.fromList(imageBytes),
    apiKey: apiKey,
  );
  @override
  Future<void> startDownload({
    required String url,
    required String destination,
    String? artworkId,
    required bool overwrite,
  }) async => bindings.startDownload(
    url: url,
    destination: destination,
    artworkId: artworkId,
    overwrite: overwrite,
  );
  @override
  Future<ConnectivityCheckResult> testConnectivity() async =>
      bindings.testConnectivity();
  @override
  Future<bool> toggleBookmark({
    required int illustId,
    required bool isBookmarked,
  }) async =>
      bindings.toggleBookmark(illustId: illustId, isBookmarked: isBookmarked);
  @override
  Future<void> updateNetworkConfig({
    required FlutterNetworkConfig config,
  }) async => bindings.updateNetworkConfig(config: config);
  @override
  Future<String> version() async => bindings.version();
}

final class _EventLease {
  _EventLease(this._cancel, this._dispose);
  final void Function() _cancel;
  final Future<void> Function() _dispose;
  bool _cancelled = false;
  Future<void>? _closed;
  void cancel() {
    if (!_cancelled) {
      _cancelled = true;
      _cancel();
    }
  }

  Future<void> close() => _closed ??= () async {
    cancel();
    await _dispose();
  }();
}
