import 'dart:async';

import 'api/download.dart';
import 'api/events.dart';

/// Each source subscription owns a unique Rust task; releasing the last Dart
/// listener cancels that task before closing FRB's receive port.
class _SharedEventStream<T> {
  _SharedEventStream(this.open, this.stop) {
    controller = StreamController<T>.broadcast(
      onListen: _start,
      onCancel: _cancel,
    );
  }
  final Stream<T> Function(String) open;
  final Future<void> Function(String) stop;
  late final StreamController<T> controller;
  StreamSubscription<T>? _subscription;
  String? _id;
  static int _nextId = 0;

  void _start() {
    final id = 'events-${DateTime.now().microsecondsSinceEpoch}-${++_nextId}';
    _id = id;
    _subscription = open(id)
        .listen(controller.add, onError: controller.addError);
  }

  void _cancel() {
    final id = _id;
    final subscription = _subscription;
    _id = null;
    _subscription = null;
    if (id != null) unawaited(_close(id, subscription));
  }

  Future<void> _close(String id, StreamSubscription<T>? subscription) async {
    try {
      await stop(id);
      await subscription?.cancel();
    } catch (error, trace) {
      if (controller.hasListener) controller.addError(error, trace);
    }
  }
}

final _sessionEvents = _SharedEventStream<SessionEvent>(
  (id) => createSessionEventStream(streamId: id),
  (id) => cancelSessionEventStream(streamId: id),
);
final _downloadEvents = _SharedEventStream<DownloadProgressEvent>(
  (id) => createDownloadEventStream(streamId: id),
  (id) => cancelDownloadEventStream(streamId: id),
);

Stream<SessionEvent> getAppSessionEventStream() =>
    _sessionEvents.controller.stream;
Stream<DownloadProgressEvent> getAppDownloadEventStream() =>
    _downloadEvents.controller.stream;
