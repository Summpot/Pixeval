import 'dart:async';
import 'dart:typed_data';

import 'pixeval.g.dart';
import 'models.dart';

abstract class PixevalApi {
  Future<void> addSearchHistory({
    required String keyword,
    String? translatedName,
  });

  Future<FilterAnalysisResultDto> analyzeFilterQuery({
    required String query,
    required int caretPos,
  });

  Future<String> asyncPing({required String message});

  Future<bool> cancelDownload({required String destination});

  Future<void> cancelDownloadEventStream({String? streamId});

  Future<void> cancelSessionEventStream({String? streamId});

  Future<void> clearAllBrowseHistory();

  Future<void> clearAllDownloads();

  Future<void> clearAllSearchHistory();

  Future<void> clearImageCache();

  Stream<DownloadProgressEvent> createDownloadEventStream({String? streamId});

  Future<PkceAuthParamsDto> createPkceAuthParams();

  Stream<SessionEvent> createSessionEventStream({String? streamId});

  Future<bool> deleteSearchHistoryItem({required String keyword});

  Future<void> exportUgoira({
    required String zipPath,
    required String outputPath,
    required String formatName,
    required List<int> frameDelaysMs,
  });

  Future<UgoiraFramesDto> extractUgoiraFramesFromZipBytes({
    required List<int> zipBytes,
    required List<int> delays,
  });

  Future<Uint8List> fetchImage({required String url, String? referer});

  Future<List<IllustSummary>> filterArtworksByQuery({
    required String query,
    required List<IllustSummary> artworks,
  });

  Future<List<BrowseHistoryItemDto>> getBrowseHistory({
    required int skip,
    required int take,
  });

  Future<CacheStatsDto> getCacheStats();

  Future<Uint8List?> getCachedImage({required String url});

  Future<UserProfile?> getCurrentUser();

  Future<FeedPageResult> getFollowFeed({
    required String restrict,
    required bool refresh,
  });

  Future<IllustSummary> getIllustDetail({required int illustId});

  Future<FlutterNetworkConfig> getNetworkConfig();

  Future<NovelContentDto> getNovelContentRendered({required int novelId});

  Future<NovelDetailDto> getNovelDetail({required int novelId});

  Future<FeedPageResult> getRankingFeed({
    required String mode,
    String? date,
    required bool refresh,
  });

  Future<FeedPageResult> getRecommendedFeed({required bool refresh});

  Future<String?> getRefreshToken();

  Future<List<AutocompleteTagDto>> getSearchAutocomplete({
    required String word,
  });

  Future<List<SearchHistoryItemDto>> getSearchHistory({required int limit});

  Future<String?> getStoredRefreshToken();

  Future<UgoiraMetadataDto> getUgoiraMetadata({required int illustId});

  Future<void> initApp();

  Future<void> initDownloadManager({required int concurrency});

  Future<void> initImageCache({
    required String cacheDir,
    BigInt? capacityBytes,
  });

  Future<void> initStorage({required String dbPath});

  Future<AuthResponse> loginByCode({
    required String code,
    required String codeVerifier,
  });

  Future<AuthResponse> loginByRefreshToken({required String refreshToken});

  Future<void> logout();

  Future<ParsedPixivLink> parsePixivUrl({required String url});

  Future<bool> pauseDownload({required String destination});

  Future<String> ping({required String message});

  Future<void> recordBrowseHistory({
    required String workId,
    required String title,
    required String previewUrl,
    required String authorName,
    String? workType,
  });

  Future<AuthResponse> refreshSession();

  Future<void> releaseSearchSession({required String sessionId});

  Future<bool> removeDownload({required String destination});

  Future<bool> resumeDownload({required String destination});

  Future<BooruSearchResultDto> searchBooru({
    required String platformName,
    required String tags,
    required int page,
  });

  Future<FeedPageResult> searchIllustrations({
    required String word,
    String? sort,
    String? searchTarget,
    required bool refresh,
    String? sessionId,
  });

  Future<List<SauceNaoItemDto>> searchSaucenao({
    required List<int> imageBytes,
    String? apiKey,
  });

  Future<void> startDownload({
    required String url,
    required String destination,
    String? artworkId,
    required bool overwrite,
  });

  Future<ConnectivityCheckResult> testConnectivity();

  Future<bool> toggleBookmark({
    required int illustId,
    required bool isBookmarked,
  });

  Future<void> updateNetworkConfig({required FlutterNetworkConfig config});

  Future<String> version();
  Future<void> close() async {}
}
