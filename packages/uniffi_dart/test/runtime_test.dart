import 'dart:async';
import 'dart:ffi';
import 'dart:typed_data';

import 'package:test/test.dart';
import 'package:uniffi_dart/uniffi_dart.dart';

void main() {
  test('64-bit platform ABI layouts match native buffer and status records',() {
    expect(sizeOf<IntPtr>(),8);
    expect(sizeOf<RustBuffer>(),24);expect(sizeOf<RustCallStatus>(),32);
    expect(sizeOf<BoltBuffer>(),32);expect(sizeOf<BoltStatus>(),4);
  });
  test('time codecs preserve microseconds and reject lossy native values',() {
    final writer=WireWriter();
    final time=DateTime.utc(2026,10,23,1,2,3,4,5);
    writeNativeTimestamp(writer,time);writeNativeDuration(writer,const Duration(seconds:42,microseconds:19));
    final reader=WireReader(writer.takeBytes());
    expect(readNativeTimestamp(reader),time);
    expect(readNativeDuration(reader),const Duration(seconds:42,microseconds:19));reader.finish();
    final invalid=WireWriter()..u64(BigInt.zero)..u32(1);
    expect(()=>readNativeDuration(WireReader(invalid.takeBytes())),throwsFormatException);
  });
  for (final endian in [Endian.big, Endian.little]) {
    test('wire roundtrip $endian including unsigned 64-bit boundaries', () {
      final w = WireWriter(endian: endian);
      final max = (BigInt.one << 64) - BigInt.one;
      w.u64(max);
      w.i64(-1);
      w.boolean(true);
      w.string('中文 🍃');
      w.optional<int>(null, w.i32);
      w.sequence([1, 2, 3], w.i32);
      w.map({'a': 42}, w.string, w.i32);
      final r = WireReader(w.takeBytes(), endian: endian);
      expect(r.u64(), max);
      expect(r.i64(), -1);
      expect(r.boolean(), true);
      expect(r.string(), '中文 🍃');
      expect(r.optional(r.i32), null);
      expect(r.sequence(r.i32), [1, 2, 3]);
      expect(r.map(r.string, r.i32), {'a': 42});
      r.finish();
    });
  }
  test('wire bounds, discriminants, UTF-8 and numeric ranges are checked', () {
    expect(() => WireReader(Uint8List(2)).i32(), throwsFormatException);
    expect(
      () => WireReader(Uint8List.fromList([2])).boolean(),
      throwsFormatException,
    );
    expect(
      () => WireReader(Uint8List.fromList([0, 0, 0, 1, 255])).string(),
      throwsFormatException,
    );
    expect(
      () => WireReader(Uint8List.fromList([1])).finish(),
      throwsFormatException,
    );
    expect(() => WireWriter().u64(BigInt.one << 64), throwsRangeError);
    expect(() => WireWriter().i8(128), throwsRangeError);
    expect(() => WireWriter().u32(-1), throwsRangeError);
  });
  test('zero-size collection elements do not require wire bytes', () {
    final w = WireWriter()..u32(3);
    expect(WireReader(w.takeBytes()).sequence(() => null), [null, null, null]);
  });
  test(
    'object release waits for calls, is idempotent, and rejects other owners',
    () async {
      final owner = Object();
      var releases = 0;
      final handle = NativeHandle(
        42,
        NativeEngine.uniffi,
        owner,
        (_) => releases++,
      );
      expect(
        () => handle.acquire(NativeEngine.boltffi, owner),
        throwsArgumentError,
      );
      expect(
        () => handle.acquire(NativeEngine.uniffi, Object()),
        throwsArgumentError,
      );
      expect(handle.acquire(NativeEngine.uniffi, owner), 42);
      final disposed = handle.dispose();
      expect(releases, 0);
      expect(
        () => handle.acquire(NativeEngine.uniffi, owner),
        throwsStateError,
      );
      handle.releaseCall();
      await disposed;
      await handle.dispose();
      expect(releases, 1);
    },
  );
  test('immediate native wake is registered before poll', () async {
    final scheduler = NativeFutureScheduler();
    var frees = 0, completions = 0;
    final result = await scheduler.run<int>(
      handle: 1,
      poll: (handle, callback, id) {
        callback.asFunction<void Function(int, int)>()(id, 0);
      },
      complete: (_) {
        completions++;
        return 42;
      },
      cancel: (_) {},
      free: (_) => frees++,
    );
    expect(result, 42);
    expect(completions, 1);
    expect(frees, 1);
    await scheduler.dispose();
  });
  test('duplicate and stale wake notifications never repeat completion/free',() async {
    final scheduler=NativeFutureScheduler();
    var polls=0,completes=0,frees=0;
    final result=await scheduler.run<int>(handle:1,poll:(_,callback,id) {
      final wake=callback.asFunction<void Function(int,int)>();
      polls++;wake(id,polls==1?1:0);wake(id,polls==1?1:0);
      if(polls>1)wake(id-1,0);
    },complete:(_){completes++;return 42;},cancel:(_){},free:(_){frees++;});
    expect(result,42);expect(completes,1);expect(frees,1);expect(polls,2);
    await scheduler.dispose();
  });
  test('failed stream poll releases its owner and callback',() async {
    var releases=0,frees=0;
    final library=Object();
    final owner=NativeHandle(1,NativeEngine.boltffi,library,(_){releases++;});
    final stream=boltEventStream<int>(subscribe:()=>1,poll:(_,_,_)=>throw StateError('poll failed'),readBatch:(_)=>[],unsubscribe:(_){},free:(_){frees++;},owner:owner,libraryOwner:library);
    await expectLater(stream,emitsError(isStateError));
    await owner.dispose();expect(releases,1);expect(frees,1);
  });
  test(
    'scheduler disposal cancels pending operations and drains callbacks',
    () async {
      final scheduler = NativeFutureScheduler();
      var frees = 0, cancels = 0;
      void Function()? wake;
      final result = scheduler.run<int>(
        handle: 1,
        poll: (handle, callback, id) {
          wake = () => callback.asFunction<void Function(int, int)>()(id, 0);
        },
        complete: (_) => 42,
        cancel: (_) {
          cancels++;
          wake!();
        },
        free: (_) => frees++,
      );
      final disposed = scheduler.dispose();
      expect(await result, 42);
      await disposed;
      await scheduler.dispose();
      expect(cancels, 1);
      expect(frees, 1);
    },
  );
  test(
    'stream cancellation interrupts a paused pending read and disposes once',
    () async {
      final pending = Completer<int?>();
      var cancels = 0, releases = 0;
      final stream = nativeEventStream<int>(
        next: () => pending.future,
        cancel: () {
          cancels++;
          pending.complete(null);
        },
        dispose: () {
          releases++;
        },
      );
      final subscription = stream.listen((_) {});
      subscription.pause();
      await subscription.cancel();
      expect(cancels, 1);
      expect(releases, 1);
    },
  );
}
