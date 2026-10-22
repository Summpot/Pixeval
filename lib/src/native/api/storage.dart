import '../pixeval.g.dart';
export '../pixeval.g.dart' show SearchHistoryItemDto, BrowseHistoryItemDto;

import '../native_api.dart';

Future<void> initStorage({required String dbPath}) =>
    NativeApi.instance.api.initStorage(dbPath: dbPath);

Future<void> addSearchHistory({
  required String keyword,
  String? translatedName,
}) => NativeApi.instance.api.addSearchHistory(
  keyword: keyword,
  translatedName: translatedName,
);

Future<List<SearchHistoryItemDto>> getSearchHistory({required int limit}) =>
    NativeApi.instance.api.getSearchHistory(limit: limit);

Future<bool> deleteSearchHistoryItem({required String keyword}) =>
    NativeApi.instance.api.deleteSearchHistoryItem(keyword: keyword);

Future<void> clearAllSearchHistory() =>
    NativeApi.instance.api.clearAllSearchHistory();

Future<void> recordBrowseHistory({
  required String workId,
  required String title,
  required String previewUrl,
  required String authorName,
  String? workType,
}) => NativeApi.instance.api.recordBrowseHistory(
  workId: workId,
  title: title,
  previewUrl: previewUrl,
  authorName: authorName,
  workType: workType,
);

Future<List<BrowseHistoryItemDto>> getBrowseHistory({
  required int skip,
  required int take,
}) => NativeApi.instance.api.getBrowseHistory(skip: skip, take: take);

Future<void> clearAllBrowseHistory() =>
    NativeApi.instance.api.clearAllBrowseHistory();
