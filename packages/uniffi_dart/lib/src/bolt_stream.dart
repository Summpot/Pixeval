import 'dart:async';
import 'dart:ffi';

import 'lifecycle.dart';

/// Drives the native BoltFFI stream poll protocol without calling its blocking
/// wait export. Each subscription has exactly one outstanding native poll.
Stream<T> boltEventStream<T>({
  required int Function() subscribe,
  required void Function(int, int, Pointer<NativeFunction<NativeWake>>) poll,
  required List<T> Function(int) readBatch,
  required void Function(int) unsubscribe,
  required void Function(int) free,
  NativeHandle? owner,
  Object? libraryOwner,
}) {
  late StreamController<T> controller;
  NativeCallable<NativeWake>? listener;
  Completer<int>? pending;
  Completer<void>? resumed;
  int? handle;
  var stopped = false;
  var generation = 0;
  Future<void>? closing;
  var retained = false;
  void Function()? unregister;
  Future<void> close() => closing ??= () async {
    stopped = true;
    resumed?.complete();
    resumed = null;
    final subscription = handle;
    if (subscription != null) unsubscribe(subscription);
    // unsubscribe acknowledges an outstanding poll with StreamPollResult.Closed.
    // Do not free the subscription or callback while that acknowledgement is queued.
    await pending?.future;
    try {
      if (subscription != null) free(subscription);
    } finally {
      listener?.close();
      listener = null;
      handle = null;
      unregister?.call();
      if (retained) {
        retained = false;
        owner!.releaseCall();
      }
    }
  }();
  Future<void> pump() async {
    try {
      if (owner != null) {
        owner.acquire(NativeEngine.boltffi, libraryOwner!);
        retained = true;
      }
      handle = subscribe();
      if (handle == 0)
        throw const NativeInteropException(
          'BoltFFI stream subscription failed',
        );
      listener = NativeCallable<NativeWake>.listener((int id, int signal) {
        if (id == generation && pending != null && !pending!.isCompleted)
          pending!.complete(signal);
      });
      unregister = owner?.onDispose(() => unawaited(close()));
      while (!stopped) {
        if (controller.isPaused) {
          resumed ??= Completer<void>();
          await resumed!.future;
          if (stopped) break;
        }
        final batch = readBatch(handle!);
        if (batch.isNotEmpty) {
          for (final value in batch) {
            controller.add(value);
          }
          continue;
        }
        final wake = Completer<int>();
        pending = wake;
        try {
          poll(handle!, ++generation, listener!.nativeFunction);
        } catch (_) {
          pending = null;
          rethrow;
        }
        final signal = await wake.future;
        pending = null;
        if (signal == 1) break;
        if (signal != 0)
          throw NativeInteropException('Unknown BoltFFI stream signal $signal');
      }
    } catch (error, stack) {
      if (!stopped) controller.addError(error, stack);
    } finally {
      await close();
      unawaited(controller.close());
    }
  }

  controller = StreamController<T>(
    onListen: () => unawaited(pump()),
    onResume: () {
      resumed?.complete();
      resumed = null;
    },
    onCancel: close,
  );
  return controller.stream;
}
