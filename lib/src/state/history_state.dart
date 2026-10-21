import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../rust/api/feed.dart';
import '../rust/api/storage.dart';

class BrowseHistoryNotifier extends Notifier<List<BrowseHistoryItemDto>> {
  @override
  List<BrowseHistoryItemDto> build() {
    Future.microtask(() => loadHistory());
    return const [];
  }

  Future<void> loadHistory() async {
    try {
      final records = await getBrowseHistory(skip: 0, take: 100);
      state = records;
    } catch (_) {}
  }

  Future<void> record(IllustSummary illust) async {
    try {
      await recordBrowseHistory(
        workId: illust.id.toString(),
        title: illust.title,
        previewUrl: illust.previewUrl,
        authorName: illust.userName,
        workType: 'illust',
      );
      await loadHistory();
    } catch (_) {}
  }

  Future<void> recordNovel({
    required int novelId,
    required String title,
    required String coverUrl,
    required String authorName,
  }) async {
    try {
      await recordBrowseHistory(
        workId: novelId.toString(),
        title: title,
        previewUrl: coverUrl,
        authorName: authorName,
        workType: 'novel',
      );
      await loadHistory();
    } catch (_) {}
  }

  Future<void> clear() async {
    try {
      await clearAllBrowseHistory();
      state = const [];
    } catch (_) {}
  }
}

final browseHistoryProvider =
    NotifierProvider<BrowseHistoryNotifier, List<BrowseHistoryItemDto>>(
  BrowseHistoryNotifier.new,
);

class SearchHistoryNotifier extends Notifier<List<SearchHistoryItemDto>> {
  @override
  List<SearchHistoryItemDto> build() {
    Future.microtask(() => load());
    return const [];
  }

  Future<void> load() async {
    try {
      final history = await getSearchHistory(limit: 30);
      state = history;
    } catch (_) {}
  }

  Future<void> record(String keyword, [String? translatedName]) async {
    try {
      await addSearchHistory(keyword: keyword, translatedName: translatedName);
      await load();
    } catch (_) {}
  }

  Future<void> delete(String keyword) async {
    try {
      await deleteSearchHistoryItem(keyword: keyword);
      state = state.where((item) => item.value != keyword).toList();
    } catch (_) {}
  }

  Future<void> clear() async {
    try {
      await clearAllSearchHistory();
      state = const [];
    } catch (_) {}
  }
}

final searchHistoryProvider =
    NotifierProvider<SearchHistoryNotifier, List<SearchHistoryItemDto>>(
  SearchHistoryNotifier.new,
);
