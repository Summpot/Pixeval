import 'dart:async';
import 'dart:ffi';

typedef _Notify = Void Function(Int64);

/// Stable Dart callback probe: worker threads may notify the creating isolate,
/// but no borrowed stack pointers or synchronous return values cross threads.
Future<List<String>> runCallbackProbe(DynamicLibrary library) async {
  final notify = library
      .lookupFunction<
        Void Function(Pointer<NativeFunction<_Notify>>, Uint32),
        void Function(Pointer<NativeFunction<_Notify>>, int)
      >('probe_notify');
  final active = library.lookupFunction<Uint32 Function(), int Function()>(
    'probe_active_threads',
  );
  final values = <int>[];
  final completed = Completer<void>();
  final callback = NativeCallable<_Notify>.listener((int value) {
    values.add(value);
    if (values.length == 100) completed.complete();
  });
  try {
    notify(callback.nativeFunction, 100);
    await completed.future.timeout(const Duration(seconds: 10));
    if (values.length != 100 || values.first != 0 || values.last != 99) {
      throw StateError('Native notification delivery was corrupted');
    }
    if (active() != 0) throw StateError('Native worker threads remain active');
    return ['worker notification ordering', 'joined native teardown'];
  } finally {
    callback.close();
  }
}
