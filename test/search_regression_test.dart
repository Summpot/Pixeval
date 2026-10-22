import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:pixeval_flutter/src/native/api/feed.dart';
import 'package:pixeval_flutter/src/native/api/search.dart';
import 'package:pixeval_flutter/src/native/native_api.dart';
import 'package:pixeval_flutter/src/state/auth_state.dart';
import 'package:pixeval_flutter/src/state/search_state.dart';
import 'package:pixeval_flutter/src/ui/search_page.dart';

import 'widget_test.dart' as mocks;

class StableAuth extends AuthNotifier {
  @override
  AuthState build() => const AuthState();
}

class ControlledApi extends mocks.MockPixevalApi {
  final autocomplete = Completer<List<AutocompleteTagDto>>();
  final requests = <String, Completer<FeedPageResult>>{};
  final sessions = <String>[];
  final released = <String>[];
  bool holdSearch = false;

  @override
  Future<List<AutocompleteTagDto>> getSearchAutocomplete({
    required String word,
  }) => autocomplete.future;

  @override
  Future<FeedPageResult> searchIllustrations({
    required String word,
    String? sort,
    String? searchTarget,
    required bool refresh,
    String? sessionId,
  }) {
    sessions.add(sessionId!);
    if (!holdSearch) {
      return Future.value(const FeedPageResult(items: [], exhausted: true));
    }
    return requests.putIfAbsent(word, Completer<FeedPageResult>.new).future;
  }

  @override
  Future<void> releaseSearchSession({required String sessionId}) async {
    released.add(sessionId);
  }
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late ControlledApi api;
  late ProviderContainer container;
  setUp(() {
    SharedPreferences.setMockInitialValues({});
    api = ControlledApi();
    NativeApi.initMock(api: api);
    container = ProviderContainer(
      overrides: [authProvider.overrideWith(StableAuth.new)],
    );
  });
  tearDown(() {
    container.dispose();
    NativeApi.dispose();
  });

  for (final action in ['empty', 'clear', 'submit']) {
    test('$action invalidates pending autocomplete', () async {
      final notifier = container.read(searchProvider.notifier);
      final pending = notifier.updateAutocomplete('cat');
      if (action == 'empty') await notifier.updateAutocomplete('');
      if (action == 'clear') notifier.clearSuggestions();
      if (action == 'submit') await notifier.search('dog');
      api.autocomplete.complete([const AutocompleteTagDto(name: 'cat')]);
      await pending;
      expect(container.read(searchProvider).suggestions, isEmpty);
      expect(container.read(searchProvider).filterCompletions, isEmpty);
    });
  }

  test('late search responses cannot replace a newer result', () async {
    api.holdSearch = true;
    final notifier = container.read(searchProvider.notifier);
    final old = notifier.search('cat');
    final latest = notifier.search('dog');
    final oldSession = api.sessions[0];
    final latestSession = api.sessions[1];
    expect(oldSession, isNot(latestSession));
    api.requests['dog']!.complete(
      const FeedPageResult(items: [], exhausted: false),
    );
    await latest;
    api.requests['cat']!.complete(
      const FeedPageResult(items: [], exhausted: false),
    );
    await old;
    expect(container.read(searchProvider).query, 'dog');
    expect(api.released, contains(oldSession));
    expect(api.released, isNot(contains(latestSession)));
    api.holdSearch = false;
    await notifier.loadMore();
    expect(api.sessions.last, latestSession);
    expect(container.read(searchProvider).exhausted, isTrue);
  });

  test(
    'disposing a page releases its Rust cursor and ignores pending work',
    () async {
      final notifier = container.read(searchProvider.notifier);
      final pending = notifier.updateAutocomplete('cat');
      await notifier.search('cat');
      final session = api.sessions.single;
      container.dispose();
      api.autocomplete.complete([const AutocompleteTagDto(name: 'cat')]);
      await pending;
      expect(api.released, contains(session));
      container = ProviderContainer();
    },
  );

  testWidgets('two SearchPages have independent state and Rust session IDs', (
    tester,
  ) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [authProvider.overrideWith(StableAuth.new)],
        child: const MaterialApp(
          home: Column(
            children: [
              Expanded(child: SearchPage(initialQuery: 'cat')),
              Expanded(child: SearchPage(initialQuery: 'dog')),
            ],
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(api.sessions.toSet(), hasLength(2));
    final inputs = tester
        .widgetList<TextField>(find.byType(TextField))
        .map((w) => w.controller!.text);
    expect(inputs, containsAll(['cat', 'dog']));
    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump();
    expect(api.released, containsAll(api.sessions));
  });
}
