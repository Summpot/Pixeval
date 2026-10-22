import 'dart:async';
import 'dart:ffi';
import 'dart:io';
import 'dart:typed_data';

import 'package:test/test.dart';
import 'package:uniffi_dart/uniffi_dart.dart';

import 'generated/fixture.g.dart';
import 'generated_bolt/fixture.g.dart' as bolt_only;
import 'generated_uni/fixture.g.dart' as uni_only;

void main() {
  final path = Platform.environment['NATIVE_FFI_FIXTURE'];
  for (final engine in NativeEngine.values) {
    group(
      engine.name,
      () {
        late NativeBindings bindings;
        setUp(() {
          bindings = NativeBindings(
            NativeLibrary.dynamic(DynamicLibrary.open(path!)),
            engine: engine,
          );
        });
        tearDown(() => bindings.dispose());
        test('scalars and all u64 bits', () {
          expect(bindings.add(a: 40, b: 2), 42);
          final max = (BigInt.one << 64) - BigInt.one;
          expect(bindings.echoU64(value: max), max);
        });
        test('direct record', () {
          final result = bindings.echoPoint(value: const Point(x: 3, y: 4));
          expect([result.x, result.y], [3, 4]);
        });
        test(
          'explicit enum discriminants, scalar options and direct vectors',
          () {
            expect(
              bindings.echoMode(value: const Mode.done()),
              const Mode.done(),
            );
            expect(
              bindings.echoMode(value: const Mode.ready()),
              const Mode.ready(),
            );
            final max = (BigInt.one << 64) - BigInt.one;
            expect(bindings.echoOptional(value: max), max);
            expect(bindings.echoOptional(), isNull);
            expect(
              bindings.echoNumbers(value: [BigInt.zero, BigInt.one << 63, max]),
              [BigInt.zero, BigInt.one << 63, max],
            );
            expect(bindings.echoNumbers(value: []), isEmpty);
            expect(
              bindings.echoBytes(value: Uint8List.fromList([0, 128, 255])),
              [0, 128, 255],
            );
            expect(
              () => bindings.echoU64(value: BigInt.one << 64),
              throwsRangeError,
            );
            expect(
              () => bindings.echoOptional(value: -BigInt.one),
              throwsRangeError,
            );
            expect(
              () => bindings.echoNumbers(value: [BigInt.one << 64]),
              throwsRangeError,
            );
          },
        );
        test('encoded nested record and owned bytes', () {
          final payload = Payload(
            name: '中文 🍃',
            bytes: Uint8List.fromList(List.generate(65536, (i) => i & 255)),
            points: [const Point(x: 3, y: 4)],
            optional: BigInt.one << 63,
          );
          final result = bindings.echoPayload(value: payload);
          expect(result.name, payload.name);
          expect(result.bytes, payload.bytes);
          expect(result.points.single.x, 3);
          expect(result.optional, payload.optional);
          expect(
            bindings
                .echoPayload(
                  value: Payload(
                    name: '',
                    bytes: Uint8List(0),
                    points: [],
                    optional: null,
                  ),
                )
                .optional,
            isNull,
          );
        });
        test('tagged union and typed errors', () {
          expect(
            bindings.echoEvent(value: Event.progress(value: BigInt.from(42))),
            isA<Event_Progress>().having(
              (e) => e.value,
              'value',
              BigInt.from(42),
            ),
          );
          expect(bindings.divide(a: 84, b: 2), 42);
          expect(
            () => bindings.divide(a: 84, b: 0),
            throwsA(
              isA<FixtureError_Invalid>().having(
                (e) => e.message,
                'message',
                'division by zero',
              ),
            ),
          );
        });
        test('fallible async calls and string errors', () async {
          expect(await bindings.asyncDivide(a: 84, b: 2), 42);
          await expectLater(
            bindings.asyncDivide(a: 84, b: 0),
            throwsA(isA<FixtureError_Invalid>()),
          );
          expect(bindings.stringError(fail: false), 'ok');
          expect(
            () => bindings.stringError(fail: true),
            throwsA('fixture string error'),
          );
        });
        test('immediate and delayed futures', () async {
          expect(
            await bindings.delayedEcho(value: 'hello', delayMs: 0),
            'hello',
          );
          expect(
            await bindings.delayedEcho(value: 'later', delayMs: 20),
            'later',
          );
        });
        test('future cancellation', () async {
          final token = NativeCancellationToken();
          final pending = bindings.delayedEcho(
            value: 'later',
            delayMs: 10000,
            cancellationToken: token,
          );
          token.cancel();
          await expectLater(pending, throwsA(isA<NativeCancelledException>()));
        });
        test('object calls and idempotent disposal', () async {
          final counter = bindings.counterNew(value: 40);
          expect(counter.increment(amount: 2), 42);
          expect(bindings.readCounter(value:counter),42);
          await counter.dispose();
          await counter.dispose();
          expect(() => counter.increment(amount: 1), throwsStateError);
        });
      test('object disposal waits for an in-flight method',() async {
        final counter=bindings.counterNew(value:42);
        final pending=counter.readLater(delayMs:25);
        var released=false;
        final disposing=counter.dispose().then((_){released=true;});
        expect(released,isFalse);
        expect(await pending,42);
        await disposing;expect(released,isTrue);
        expect(()=>counter.increment(amount:1),throwsStateError);
      });
      },
      skip: path == null
          ? 'Set NATIVE_FFI_FIXTURE to the compiled native fixture'
          : false,
    );
  }
  test(
    'BoltFFI native stream waits asynchronously and closes with its owner',
    () async {
      if (path == null) return;
      final bindings = NativeBindings(
        NativeLibrary.dynamic(DynamicLibrary.open(path)),
        engine: NativeEngine.boltffi,
      );
      final ticker = bindings.tickerNew();
      final values = <int>[];
      final ready = Completer<void>();
      final done = Completer<void>();
      final subscription = ticker.updates().listen((value) {
        values.add(value);
        if (values.length == 3) ready.complete();
      }, onDone: done.complete);
      ticker.emit(value: 1);
      ticker.emit(value: 2);
      ticker.emit(value: 3);
      await ready.future.timeout(const Duration(seconds: 5));
      expect(values, [1, 2, 3]);
      await ticker.dispose();
      await done.future.timeout(const Duration(seconds: 5));
      await subscription.cancel();
      await bindings.dispose();
    },
    skip: path == null,
  );
  test('BoltFFI-only contracts never resolve UniFFI symbols', () async {
    if (path == null) return;
    final library = DynamicLibrary.open(path);
    final bindings = bolt_only.NativeBindings(
      NativeLibrary((symbol) {
        if (symbol.startsWith('uniffi_') || symbol.startsWith('ffi_'))
          throw StateError('Unexpected UniFFI dependency: $symbol');
        return library.lookup<Void>(symbol);
      }),
    );
    expect(bindings.add(a: 40, b: 2), 42);
    expect(
      bindings
          .echoPayload(
            value: bolt_only.Payload(
              name: 'standalone',
              bytes: Uint8List.fromList([1, 2]),
              points: [const bolt_only.Point(x: 3, y: 4)],
              optional: null,
            ),
          )
          .name,
      'standalone',
    );
    expect(await bindings.delayedEcho(value: 'async', delayMs: 1), 'async');
    final counter = bindings.counterNew(value: 40);
    expect(counter.increment(amount: 2), 42);
    await bindings.dispose();
    expect(() => counter.increment(amount: 1), throwsStateError);
  }, skip: path == null);
  test('UniFFI-only contracts never resolve BoltFFI symbols', () async {
    if (path == null) return;
    final library = DynamicLibrary.open(path);
    final bindings = uni_only.NativeBindings(
      NativeLibrary((symbol) {
        if (symbol.startsWith('boltffi_'))
          throw StateError('Unexpected BoltFFI dependency: $symbol');
        return library.lookup<Void>(symbol);
      }),
    );
    expect(
      bindings.echoMode(value: const uni_only.Mode.done()),
      const uni_only.Mode.done(),
    );
    expect(bindings.echoOptional(value: BigInt.one << 63), BigInt.one << 63);
    expect(
      await bindings.delayedEcho(value: 'standalone', delayMs: 1),
      'standalone',
    );
    await bindings.dispose();
    expect(() => bindings.add(a: 1, b: 2), throwsStateError);
  }, skip: path == null);
  test('dual views reject crossing object handles',() async {
    if(path==null)return;
    final bindings=NativeBindings(NativeLibrary.dynamic(DynamicLibrary.open(path)));
    final uni=bindings.uniffi.counterNew(value:1),bolt=bindings.boltffi.counterNew(value:2);
    expect(()=>bindings.boltffi.readCounter(value:uni),throwsArgumentError);
    expect(()=>bindings.uniffi.readCounter(value:bolt),throwsArgumentError);
    expect(bindings.uniffi.readCounter(value:uni),1);
    expect(bindings.boltffi.readCounter(value:bolt),2);
    await bindings.dispose();
    expect(()=>bindings.boltffi,throwsStateError);
  },skip:path==null);
}
