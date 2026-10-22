import '../models.dart';
export '../models.dart';

import '../native_api.dart';

Future<FeedPageResult> getRecommendedFeed({required bool refresh}) =>
    NativeApi.instance.api.getRecommendedFeed(refresh: refresh);

Future<FeedPageResult> getRankingFeed({
  required String mode,
  String? date,
  required bool refresh,
}) => NativeApi.instance.api.getRankingFeed(
  mode: mode,
  date: date,
  refresh: refresh,
);

Future<FeedPageResult> getFollowFeed({
  required String restrict,
  required bool refresh,
}) =>
    NativeApi.instance.api.getFollowFeed(restrict: restrict, refresh: refresh);

Future<bool> toggleBookmark({
  required int illustId,
  required bool isBookmarked,
}) => NativeApi.instance.api.toggleBookmark(
  illustId: illustId,
  isBookmarked: isBookmarked,
);

Future<IllustSummary> getIllustDetail({required int illustId}) =>
    NativeApi.instance.api.getIllustDetail(illustId: illustId);
