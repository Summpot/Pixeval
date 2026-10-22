import '../pixeval.g.dart';
import '../models.dart';
export '../pixeval.g.dart' show AutocompleteTagDto;

import '../native_api.dart';

Future<void> releaseSearchSession({required String sessionId}) =>
    NativeApi.instance.api.releaseSearchSession(sessionId: sessionId);

Future<List<AutocompleteTagDto>> getSearchAutocomplete({
  required String word,
}) => NativeApi.instance.api.getSearchAutocomplete(word: word);

Future<FeedPageResult> searchIllustrations({
  required String word,
  String? sort,
  String? searchTarget,
  required bool refresh,
  String? sessionId,
}) => NativeApi.instance.api.searchIllustrations(
  word: word,
  sort: sort,
  searchTarget: searchTarget,
  refresh: refresh,
  sessionId: sessionId,
);
