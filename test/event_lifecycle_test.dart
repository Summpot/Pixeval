import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:pixeval_flutter/src/rust/api/events.dart';
import 'package:pixeval_flutter/src/rust/frb_generated.dart';
import 'package:pixeval_flutter/src/rust/stream_helpers.dart';

import 'widget_test.dart' as mocks;

class EventApi extends mocks.MockRustLibApi {
  final sources = <String, StreamController<SessionEvent>>{};
  final stopped = <String>[];
  @override
  Stream<SessionEvent> crateApiEventsCreateSessionEventStream({
    String? streamId,
  }) {
    final controller = StreamController<SessionEvent>();
    sources[streamId!] = controller;
    return controller.stream;
  }

  @override
  Future<void> crateApiEventsCancelSessionEventStream({
    String? streamId,
  }) async {
    stopped.add(streamId!);
    await sources[streamId]?.close();
  }
}

void main() {
  test('last broadcast listener releases Rust and a later listener opens a new source', () async {
    final api = EventApi();
    RustLib.initMock(api: api);
    try {
      final first = getAppSessionEventStream().listen((_) {});
      final event = Completer<SessionEvent>();
      final second = getAppSessionEventStream().listen(event.complete);
      expect(api.sources, hasLength(1));
      final oldId = api.sources.keys.single;
      await first.cancel();
      expect(api.stopped, isEmpty);
      api.sources[oldId]!.add(
        SessionEvent.rateLimited(retryAfterSeconds: BigInt.one),
      );
      await event.future;
      await second.cancel();
      await Future<void>.delayed(Duration.zero);
      expect(api.stopped, [oldId]);
      final third = getAppSessionEventStream().listen((_) {});
      expect(api.sources, hasLength(2));
      expect(api.sources.keys.last, isNot(oldId));
      await third.cancel();
      await Future<void>.delayed(Duration.zero);
      expect(api.stopped, hasLength(2));
    } finally {
      RustLib.dispose();
    }
  });
}
