import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../rust/api/feed.dart';
import 'auth_state.dart';

IllustSummary updateIllustBookmark(IllustSummary item, bool isBookmarked) {
  return IllustSummary(
    id: item.id,
    title: item.title,
    caption: item.caption,
    userId: item.userId,
    userName: item.userName,
    userAccount: item.userAccount,
    userAvatarUrl: item.userAvatarUrl,
    previewUrl: item.previewUrl,
    originalUrl: item.originalUrl,
    width: item.width,
    height: item.height,
    pageCount: item.pageCount,
    isBookmarked: isBookmarked,
    totalBookmarks:
        item.totalBookmarks +
        (item.isBookmarked == isBookmarked ? 0 : (isBookmarked ? 1 : -1)),
    totalViews: item.totalViews,
    tags: item.tags,
    isR18: item.isR18,
    isUgoira: item.isUgoira,
    pageUrls: item.pageUrls,
    originalPageUrls: item.originalPageUrls,
    thumbnailUrl: item.thumbnailUrl,
    filterMetadata: item.filterMetadata,
  );
}

class FeedState {
  final List<IllustSummary> items;
  final bool isLoading;
  final bool isRefreshing;
  final bool exhausted;
  final String? errorMessage;

  const FeedState({
    this.items = const [],
    this.isLoading = false,
    this.isRefreshing = false,
    this.exhausted = false,
    this.errorMessage,
  });

  FeedState copyWith({
    List<IllustSummary>? items,
    bool? isLoading,
    bool? isRefreshing,
    bool? exhausted,
    String? errorMessage,
  }) {
    return FeedState(
      items: items ?? this.items,
      isLoading: isLoading ?? this.isLoading,
      isRefreshing: isRefreshing ?? this.isRefreshing,
      exhausted: exhausted ?? this.exhausted,
      errorMessage: errorMessage,
    );
  }
}

class RecommendedFeedNotifier extends Notifier<FeedState> {
  @override
  FeedState build() {
    ref.listen(authProvider, (previous, next) {
      if ((previous == null || !previous.isLoggedIn) && next.isLoggedIn) {
        loadInitial();
      }
    });
    Future.microtask(() => loadInitial());
    return const FeedState();
  }

  Future<void> loadInitial() async {
    if (!ref.read(authProvider).isLoggedIn) return;
    state = state.copyWith(isLoading: true, errorMessage: null);
    try {
      final res = await getRecommendedFeed(refresh: true);
      state = state.copyWith(
        items: res.items,
        exhausted: res.exhausted,
        isLoading: false,
      );
    } catch (e) {
      state = state.copyWith(isLoading: false, errorMessage: e.toString());
    }
  }

  Future<void> refresh() async {
    if (!ref.read(authProvider).isLoggedIn) return;
    state = state.copyWith(isRefreshing: true, errorMessage: null);
    try {
      final res = await getRecommendedFeed(refresh: true);
      state = state.copyWith(
        items: res.items,
        exhausted: res.exhausted,
        isRefreshing: false,
      );
    } catch (e) {
      state = state.copyWith(isRefreshing: false, errorMessage: e.toString());
    }
  }

  Future<void> loadMore() async {
    if (!ref.read(authProvider).isLoggedIn) return;
    if (state.isLoading || state.isRefreshing || state.exhausted) return;
    state = state.copyWith(isLoading: true);
    try {
      final res = await getRecommendedFeed(refresh: false);
      final newItems = [...state.items, ...res.items];
      state = state.copyWith(
        items: newItems,
        exhausted: res.exhausted,
        isLoading: false,
      );
    } catch (e) {
      state = state.copyWith(isLoading: false, errorMessage: e.toString());
    }
  }

  void updateItemBookmarkLocally(int illustId, bool isBookmarked) {
    final updatedList = state.items.map((item) {
      if (item.id == illustId) {
        return updateIllustBookmark(item, isBookmarked);
      }
      return item;
    }).toList();
    state = state.copyWith(items: updatedList);
  }

  Future<void> toggleItemBookmark(
    int illustId,
    bool currentlyBookmarked,
  ) async {
    try {
      final newStatus = await toggleBookmark(
        illustId: illustId,
        isBookmarked: currentlyBookmarked,
      );
      updateItemBookmarkLocally(illustId, newStatus);
    } catch (_) {}
  }
}

final recommendedFeedProvider =
    NotifierProvider<RecommendedFeedNotifier, FeedState>(
      RecommendedFeedNotifier.new,
    );

class RankingFeedNotifier extends Notifier<FeedState> {
  String _mode = 'day';

  String get currentMode => _mode;
  String get mode => _mode;

  @override
  FeedState build() {
    ref.listen(authProvider, (previous, next) {
      if ((previous == null || !previous.isLoggedIn) && next.isLoggedIn) {
        loadInitial();
      }
    });
    Future.microtask(() => loadInitial());
    return const FeedState();
  }

  Future<void> setMode(String mode) async {
    if (_mode == mode) return;
    _mode = mode;
    await loadInitial();
  }

  Future<void> loadInitial() async {
    if (!ref.read(authProvider).isLoggedIn) return;
    state = state.copyWith(isLoading: true, errorMessage: null);
    try {
      final res = await getRankingFeed(mode: _mode, refresh: true);
      state = state.copyWith(
        items: res.items,
        exhausted: res.exhausted,
        isLoading: false,
      );
    } catch (e) {
      state = state.copyWith(isLoading: false, errorMessage: e.toString());
    }
  }

  Future<void> refresh() async {
    if (!ref.read(authProvider).isLoggedIn) return;
    state = state.copyWith(isRefreshing: true, errorMessage: null);
    try {
      final res = await getRankingFeed(mode: _mode, refresh: true);
      state = state.copyWith(
        items: res.items,
        exhausted: res.exhausted,
        isRefreshing: false,
      );
    } catch (e) {
      state = state.copyWith(isRefreshing: false, errorMessage: e.toString());
    }
  }

  Future<void> loadMore() async {
    if (!ref.read(authProvider).isLoggedIn) return;
    if (state.isLoading || state.isRefreshing || state.exhausted) return;
    state = state.copyWith(isLoading: true);
    try {
      final res = await getRankingFeed(mode: _mode, refresh: false);
      final newItems = [...state.items, ...res.items];
      state = state.copyWith(
        items: newItems,
        exhausted: res.exhausted,
        isLoading: false,
      );
    } catch (e) {
      state = state.copyWith(isLoading: false, errorMessage: e.toString());
    }
  }

  void updateItemBookmarkLocally(int illustId, bool isBookmarked) {
    final updatedList = state.items.map((item) {
      if (item.id == illustId) {
        return updateIllustBookmark(item, isBookmarked);
      }
      return item;
    }).toList();
    state = state.copyWith(items: updatedList);
  }

  Future<void> toggleItemBookmark(
    int illustId,
    bool currentlyBookmarked,
  ) async {
    try {
      final newStatus = await toggleBookmark(
        illustId: illustId,
        isBookmarked: currentlyBookmarked,
      );
      updateItemBookmarkLocally(illustId, newStatus);
    } catch (_) {}
  }
}

final rankingFeedProvider = NotifierProvider<RankingFeedNotifier, FeedState>(
  RankingFeedNotifier.new,
);

class FollowFeedNotifier extends Notifier<FeedState> {
  String _restrict = 'all';

  String get restrict => _restrict;

  void setRestrict(String value) {
    _restrict = value;
    refresh();
  }

  @override
  FeedState build() {
    ref.listen(authProvider, (previous, next) {
      if ((previous == null || !previous.isLoggedIn) && next.isLoggedIn) {
        loadInitial();
      }
    });
    Future.microtask(() => loadInitial());
    return const FeedState();
  }

  Future<void> loadInitial() async {
    if (!ref.read(authProvider).isLoggedIn) return;
    state = state.copyWith(isLoading: true, errorMessage: null);
    try {
      final res = await getFollowFeed(restrict: _restrict, refresh: true);
      state = state.copyWith(
        items: res.items,
        exhausted: res.exhausted,
        isLoading: false,
      );
    } catch (e) {
      state = state.copyWith(isLoading: false, errorMessage: e.toString());
    }
  }

  Future<void> refresh() async {
    if (!ref.read(authProvider).isLoggedIn) return;
    state = state.copyWith(isRefreshing: true, errorMessage: null);
    try {
      final res = await getFollowFeed(restrict: _restrict, refresh: true);
      state = state.copyWith(
        items: res.items,
        exhausted: res.exhausted,
        isRefreshing: false,
      );
    } catch (e) {
      state = state.copyWith(isRefreshing: false, errorMessage: e.toString());
    }
  }

  Future<void> loadMore() async {
    if (!ref.read(authProvider).isLoggedIn) return;
    if (state.isLoading || state.isRefreshing || state.exhausted) return;
    state = state.copyWith(isLoading: true);
    try {
      final res = await getFollowFeed(restrict: _restrict, refresh: false);
      final newItems = [...state.items, ...res.items];
      state = state.copyWith(
        items: newItems,
        exhausted: res.exhausted,
        isLoading: false,
      );
    } catch (e) {
      state = state.copyWith(isLoading: false, errorMessage: e.toString());
    }
  }

  void updateItemBookmarkLocally(int illustId, bool isBookmarked) {
    final updatedList = state.items.map((item) {
      if (item.id == illustId) {
        return updateIllustBookmark(item, isBookmarked);
      }
      return item;
    }).toList();
    state = state.copyWith(items: updatedList);
  }

  Future<void> toggleItemBookmark(
    int illustId,
    bool currentlyBookmarked,
  ) async {
    try {
      final newStatus = await toggleBookmark(
        illustId: illustId,
        isBookmarked: currentlyBookmarked,
      );
      updateItemBookmarkLocally(illustId, newStatus);
    } catch (_) {}
  }
}

final followFeedProvider = NotifierProvider<FollowFeedNotifier, FeedState>(
  FollowFeedNotifier.new,
);
