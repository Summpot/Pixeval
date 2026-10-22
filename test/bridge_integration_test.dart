import 'dart:ffi';
import 'dart:async';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:pixeval_flutter/src/native/native_api.dart';
import 'package:pixeval_flutter/src/native/api/auth.dart';
import 'package:pixeval_flutter/src/native/api/download.dart';
import 'package:pixeval_flutter/src/native/api/events.dart';
import 'package:pixeval_flutter/src/native/api/filters.dart';

import 'widget_test.dart' as mocks;

const libraryPath = String.fromEnvironment('PIXEVAL_NATIVE_LIBRARY');

void main() {
  for (final engine in NativeEngine.values) {
    group(
      'real native ${engine.name}',
      () {
        setUpAll(
          () => NativeApi.init(
            library: DynamicLibrary.open(libraryPath),
            engine: engine,
          ),
        );
        tearDownAll(NativeApi.dispose);
        test('PKCE and shorthand filtering cross the real FFI', () async {
          final params = await createPkceAuthParams();
          expect(params.codeVerifier.length, 43);
          final sample = await mocks.MockPixevalApi().getIllustDetail(
            illustId: 1,
          );
          expect(
            await filterArtworksByQuery(query: '#test', artworks: [sample]),
            hasLength(1),
          );
          expect(
            await filterArtworksByQuery(query: '#missing', artworks: [sample]),
            isEmpty,
          );
          await expectLater(
            filterArtworksByQuery(query: '(tag:test', artworks: [sample]),
            throwsA(anything),
          );
        });
        test('session stream subscribes without a runtime panic', () async {
          Object? error;
          final sub = createSessionEventStream().listen(
            (_) {},
            onError: (Object e) {
              error = e;
            },
          );
          await Future<void>.delayed(const Duration(milliseconds: 100));
          await cancelSessionEventStream();
          await sub.cancel();
          expect(error, isNull);
        });
        test('download stream delivers completion and cancels artwork-key tasks', () async {
          final directory = await Directory.systemTemp.createTemp(
            'pixeval-bridge-test-',
          );
          final server = await HttpServer.bind(InternetAddress.loopbackIPv4, 0);
          server.listen((request) async {
            if (request.uri.path == '/hold') return;
            request.response.add(List<int>.filled(8192, 42));
            await request.response.close();
          });
          final destination = '${directory.path}/image.bin';
          final completed = Completer<void>();
          final sub = createDownloadEventStream().listen((event) {
            event.when(
              progressUpdated: (_, _, _, _) {},
              stateChanged: (_, _, _) {},
              completed: (path) {
                if (path == destination && !completed.isCompleted) {
                  completed.complete();
                }
              },
            );
          });
          try {
            await initDownloadManager(concurrency: 2);
            await Future<void>.delayed(const Duration(milliseconds: 100));
            await startDownload(
              url: 'http://127.0.0.1:${server.port}/image',
              destination: destination,
              artworkId: '12345',
              overwrite: true,
            );
            await completed.future.timeout(const Duration(seconds: 5));
            expect(await File(destination).length(), 8192);
            final cancelled = '${directory.path}/cancelled.bin';
            await startDownload(
              url: 'http://127.0.0.1:${server.port}/hold',
              destination: cancelled,
              artworkId: '67890',
              overwrite: true,
            );
            expect(await cancelDownload(destination: cancelled), isTrue);
          } finally {
            await clearAllDownloads();
            await cancelDownloadEventStream();
            await sub.cancel();
            await server.close(force: true);
            // Every path is created under this test-owned temporary directory.
            await directory.delete(recursive: true);
          }
        });
      },
      skip: libraryPath.isEmpty
          ? 'Set PIXEVAL_NATIVE_LIBRARY to the freshly built native library'
          : false,
    );
  }
}
