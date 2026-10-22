import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../native/api/feed.dart';
import '../native/api/filters.dart';
import '../native/api/search.dart';
import 'feed_state.dart';
import 'auth_state.dart';

class SearchState {
  final String query;
  final String sort;
  final String searchTarget;
  final List<AutocompleteTagDto> suggestions;
  final List<FilterCompletionDto> filterCompletions;
  final List<IllustSummary> items;
  final bool isLoading;
  final bool isSuggesting;
  final bool exhausted;
  final String? errorMessage;

  const SearchState({
    this.query = '',
    this.sort = 'date_desc',
    this.searchTarget = 'partial_match_for_tags',
    this.suggestions = const [],
    this.filterCompletions = const [],
    this.items = const [],
    this.isLoading = false,
    this.isSuggesting = false,
    this.exhausted = false,
    this.errorMessage,
  });

  SearchState copyWith({
    String? query,
    String? sort,
    String? searchTarget,
    List<AutocompleteTagDto>? suggestions,
    List<FilterCompletionDto>? filterCompletions,
    List<IllustSummary>? items,
    bool? isLoading,
    bool? isSuggesting,
    bool? exhausted,
    String? errorMessage,
  }) {
    return SearchState(
      query: query ?? this.query,
      sort: sort ?? this.sort,
      searchTarget: searchTarget ?? this.searchTarget,
      suggestions: suggestions ?? this.suggestions,
      filterCompletions: filterCompletions ?? this.filterCompletions,
      items: items ?? this.items,
      isLoading: isLoading ?? this.isLoading,
      isSuggesting: isSuggesting ?? this.isSuggesting,
      exhausted: exhausted ?? this.exhausted,
      errorMessage: errorMessage,
    );
  }
}

class SearchNotifier extends Notifier<SearchState> {
  static int _nextSessionId = 0;
  late String _sessionId;
  int _searchGeneration = 0;
  int _autocompleteGeneration = 0;

  @override
  SearchState build() {
    ref.watch(authProvider.select((auth) => auth.user?.id));
    _searchGeneration++;
    _autocompleteGeneration++;
    final sessionId = 'search-${++_nextSessionId}';
    _sessionId = sessionId;
    ref.onDispose(() {
      _searchGeneration++;
      _autocompleteGeneration++;
      _releaseSession(_sessionId);
    });
    return const SearchState();
  }

  void _releaseSession(String sessionId) {
    releaseSearchSession(sessionId: sessionId).catchError((Object _) {});
  }

  void _releaseStaleSession(String sessionId) {
    if (!ref.mounted || sessionId != _sessionId) {
      _releaseSession(sessionId);
    }
  }

  Future<void> updateAutocomplete(String word, {int? caretPosition}) async {
    final gen = ++_autocompleteGeneration;
    final trimmed = word.trim();
    if (trimmed.isEmpty) {
      state = state.copyWith(
        suggestions: [],
        filterCompletions: [],
        isSuggesting: false,
      );
      return;
    }
    state = state.copyWith(isSuggesting: true);
    try {
      final tagsRequest = getSearchAutocomplete(word: trimmed)
          .catchError((Object _) => <AutocompleteTagDto>[]);
      final analysis = await analyzeFilterQuery(
        query: word,
        caretPos: caretPosition ?? word.length,
      );
      if (!ref.mounted || gen != _autocompleteGeneration) return;
      // Local syntax completion should not wait for a network round-trip.
      state = state.copyWith(filterCompletions: analysis.completions);
      final tags = await tagsRequest;
      if (!ref.mounted || gen != _autocompleteGeneration) return;

      state = state.copyWith(
        suggestions: tags,
        filterCompletions: analysis.completions,
        isSuggesting: false,
      );
    } catch (_) {
      if (ref.mounted && gen == _autocompleteGeneration) {
        state = state.copyWith(
          suggestions: [],
          filterCompletions: [],
          isSuggesting: false,
        );
      }
    }
  }

  void clearSuggestions() {
    _autocompleteGeneration++;
    state = state.copyWith(
      suggestions: [],
      filterCompletions: [],
      isSuggesting: false,
    );
  }

  Future<void> search(String keyword, {bool refresh = true}) async {
    clearSuggestions();
    final cleanWord = keyword.trim();
    if (cleanWord.isEmpty) return;

    final gen = ++_searchGeneration;
    // A new query owns a new cursor even if FRB workers start out of order.
    // Pagination keeps that cursor until the next query or page disposal.
    if (refresh || cleanWord != state.query) {
      _releaseSession(_sessionId);
      _sessionId = 'search-${++_nextSessionId}';
    }
    final sessionId = _sessionId;

    state = state.copyWith(
      query: cleanWord,
      isLoading: true,
      errorMessage: null,
      suggestions: [],
      filterCompletions: [],
    );

    try {
      final result = await searchIllustrations(
        word: cleanWord,
        sort: state.sort,
        searchTarget: state.searchTarget,
        refresh: refresh,
        sessionId: sessionId,
      );

      if (!ref.mounted || gen != _searchGeneration) {
        _releaseStaleSession(sessionId);
        return;
      }

      state = state.copyWith(
        items: result.items,
        exhausted: result.exhausted,
        isLoading: false,
      );
    } catch (e) {
      if (ref.mounted && gen == _searchGeneration) {
        state = state.copyWith(isLoading: false, errorMessage: e.toString());
      } else {
        _releaseStaleSession(sessionId);
      }
    }
  }

  Future<void> loadMore() async {
    if (state.isLoading || state.exhausted || state.query.isEmpty) return;

    final gen = _searchGeneration;
    final sessionId = _sessionId;
    final query = state.query;
    state = state.copyWith(isLoading: true);
    try {
      final result = await searchIllustrations(
        word: query,
        sort: state.sort,
        searchTarget: state.searchTarget,
        refresh: false,
        sessionId: sessionId,
      );

      if (!ref.mounted || gen != _searchGeneration) {
        _releaseStaleSession(sessionId);
        return;
      }

      final combined = [...state.items, ...result.items];
      state = state.copyWith(
        items: combined,
        exhausted: result.exhausted,
        isLoading: false,
      );
    } catch (e) {
      if (ref.mounted && gen == _searchGeneration) {
        state = state.copyWith(isLoading: false, errorMessage: e.toString());
      } else {
        _releaseStaleSession(sessionId);
      }
    }
  }

  void updateItemBookmarkLocally(int illustId, bool isBookmarked) {
    final updated = state.items.map((item) {
      if (item.id == illustId) {
        return updateIllustBookmark(item, isBookmarked);
      }
      return item;
    }).toList();
    state = state.copyWith(items: updated);
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

  void setSort(String newSort) {
    if (state.sort == newSort) return;
    state = state.copyWith(sort: newSort);
    if (state.query.isNotEmpty) {
      search(state.query, refresh: true);
    }
  }

  void setSearchTarget(String newTarget) {
    if (state.searchTarget == newTarget) return;
    state = state.copyWith(searchTarget: newTarget);
    if (state.query.isNotEmpty) {
      search(state.query, refresh: true);
    }
  }
}

final searchProvider = NotifierProvider<SearchNotifier, SearchState>(
  SearchNotifier.new,
);
