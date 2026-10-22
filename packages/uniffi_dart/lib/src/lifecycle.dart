import 'dart:async';
import 'dart:ffi';

enum NativeEngine { uniffi, boltffi }

class NativeInteropException implements Exception {
  const NativeInteropException(this.message);
  final String message;

  @override
  String toString() => 'NativeInteropException: $message';
}

class NativeCancelledException extends NativeInteropException {
  const NativeCancelledException() : super('Native operation cancelled');
}

/// Cancellation is explicit; abandoning a Future does not cancel native work.
final class NativeCancellationToken {
  bool _cancelled = false;
  final _listeners = <void Function()>{};

  bool get isCancelled => _cancelled;

  void throwIfCancelled() {
    if (_cancelled) {
      throw const NativeCancelledException();
    }
  }

  void cancel() {
    if (_cancelled) {
      return;
    }
    _cancelled = true;
    for (final listener in List.of(_listeners)) {
      listener();
    }
    _listeners.clear();
  }

  void Function() register(void Function() listener) {
    if (_cancelled) {
      listener();
      return () {};
    }
    _listeners.add(listener);
    return () => _listeners.remove(listener);
  }
}

typedef NativeWake = Void Function(Uint64, Int8);

/// One stable listener per owner, shared by both engines. Wake callbacks only
/// carry integers: no stack pointers or borrowed buffers cross the SendPort.
final class NativeFutureScheduler {
  NativeFutureScheduler() {
    _listener = NativeCallable<NativeWake>.listener((int id, int signal) {
      final completion = _pending.remove(id);
      if (completion != null) {
        completion.complete(signal);
      }
    });
  }

  late final NativeCallable<NativeWake> _listener;
  final _pending = <int, Completer<int>>{};
  final _cancellations = <void Function()>{};
  final _drained = <Completer<void>>[];
  int _next = 0;
  int _active = 0;
  bool _closing = false;
  Future<void>? _disposeFuture;

  Future<T> run<T>({
    required int handle,
    required void Function(int, Pointer<NativeFunction<NativeWake>>, int) poll,
    required T Function(int) complete,
    required void Function(int) cancel,
    required void Function(int) free,
    NativeCancellationToken? cancellationToken,
  }) async {
    if (_closing) {
      free(handle);
      throw StateError('Native library is closing');
    }
    _active++;
    var cancellationSent = false;
    void requestCancel() {
      if (!cancellationSent) {
        cancellationSent = true;
        cancel(handle);
      }
    }

    final unregister = cancellationToken?.register(requestCancel);
    _cancellations.add(requestCancel);
    try {
      while (true) {
        // Register before poll: a ready future may invoke its callback inline.
        final id = ++_next;
        final completion = Completer<int>();
        _pending[id] = completion;
        try {
          poll(handle, _listener.nativeFunction, id);
        } catch (_) {
          _pending.remove(id);
          rethrow;
        }
        final signal = await completion.future;
        if (signal == 0) {
          cancellationToken?.throwIfCancelled();
          return complete(handle);
        }
        if (signal != 1) {
          throw NativeInteropException('Invalid future signal: $signal');
        }
      }
    } finally {
      unregister?.call();
      _cancellations.remove(requestCancel);
      try {
        free(handle);
      } finally {
        if (--_active == 0) {
          for (final waiter in _drained) {
            waiter.complete();
          }
          _drained.clear();
        }
      }
    }
  }

  /// Call after cancelling application operations. Keep callback trampolines
  /// alive until the native protocol has acknowledged all in-flight polls.
  Future<void> dispose() => _disposeFuture ??= _dispose();

  Future<void> _dispose() async {
    _closing = true;
    for (final cancel in _cancellations.toList()) {
      cancel();
    }
    if (_active != 0) {
      final drained = Completer<void>();
      _drained.add(drained);
      await drained.future;
    }
    _listener.close();
  }
}

/// An owned handle cannot be shared between FFI engines or library instances.
final class NativeHandle {
  NativeHandle(
    this.address,
    this.engine,
    this.owner,
    void Function(int) release,
  ) : _release = release {
    _finalizer.attach(this, (address, release), detach: this);
  }

  final int address;
  final NativeEngine engine;
  final Object owner;
  final void Function(int) _release;

  static final _finalizer = Finalizer<(int, void Function(int))>((value) {
    value.$2(value.$1);
  });

  int _active = 0;
  bool _closing = false;
  bool _released = false;
  Completer<void>? _disposed;
  final _disposeListeners = <void Function()>{};

  void Function() onDispose(void Function() listener) {
    if (_closing) {
      listener();
      return () {};
    }
    _disposeListeners.add(listener);
    return () => _disposeListeners.remove(listener);
  }

  int acquire(NativeEngine expected, Object library) {
    if (_closing) {
      throw StateError('Native object has been disposed');
    }
    if (engine != expected || !identical(owner, library)) {
      throw ArgumentError(
        'Object belongs to a different engine or native library',
      );
    }
    _active++;
    return address;
  }

  void releaseCall() {
    if (_active == 0) {
      throw StateError('Unbalanced native object lease');
    }
    _active--;
    if (_closing && _active == 0) {
      _finish();
    }
  }

  Future<void> dispose() {
    if (_closing) {
      return _disposed!.future;
    }
    _closing = true;
    _disposed ??= Completer<void>();
    for (final listener in _disposeListeners.toList()) {
      listener();
    }
    _disposeListeners.clear();
    if (_active == 0) {
      _finish();
    }
    return _disposed!.future;
  }

  void _finish() {
    if (_released) {
      return;
    }
    _released = true;
    _finalizer.detach(this);
    try {
      _release(address);
      _disposed!.complete();
    } catch (error, stack) {
      _disposed!.completeError(error, stack);
    }
  }
}

/// Explicit pull-based stream adapter. Cancellation first interrupts next(),
/// then releases its receiver, including when a subscription is paused.
Stream<T> nativeEventStream<T>({
  required Future<T?> Function() next,
  required FutureOr<void> Function() cancel,
  required FutureOr<void> Function() dispose,
}) {
  late StreamController<T> controller;
  var stopped = false;
  Completer<void>? resumed;
  Future<void>? closing;

  Future<void> performClose() async {
    stopped = true;
    resumed?.complete();
    resumed = null;
    try {
      await cancel();
    } finally {
      await dispose();
    }
  }

  Future<void> close() => closing ??= performClose();

  Future<void> pump() async {
    try {
      while (!stopped) {
        if (controller.isPaused) {
          resumed ??= Completer<void>();
          await resumed!.future;
          if (stopped) {
            break;
          }
        }
        final item = await next();
        if (stopped || item == null) {
          break;
        }
        controller.add(item);
      }
    } catch (error, stack) {
      if (!stopped) {
        controller.addError(error, stack);
      }
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
