import 'dart:async';
import 'dart:convert';
import 'dart:ffi';
import 'dart:io';

import 'package:pixeval_flutter/src/native/models.dart';
import 'package:pixeval_flutter/src/native/pixeval.g.dart';
import 'package:pixeval_flutter/src/native/service.dart';
import 'package:uniffi_dart/uniffi_dart.dart';

/// Run after build_runner: `dart run tool/benchmark_native.dart library output.json`.
/// Payloads and iteration counts match the saved pre-migration baseline.
Future<void> main(List<String> args) async {
  if (args.length != 2)
    throw ArgumentError('benchmark_native <library> <output.json>');
  final results = <Map<String, Object>>[];
  for (final engine in NativeEngine.values) {
    final api = PixevalService(
      PixevalNative(
        NativeLibrary.dynamic(DynamicLibrary.open(args[0])),
        engine: engine,
      ),
    );
    final result = <String, Object>{
      'engine': engine.name,
      'mode': 'Dart JIT, native debug',
      'sdk': Platform.version,
    };
    Future<void> measure(
      String key,
      int count,
      Future<Object?> Function() call,
    ) async {
      for (var i = 0; i < 10; i++) {
        await call();
      }
      final watch = Stopwatch()..start();
      for (var i = 0; i < count; i++) {
        await call();
      }
      watch.stop();
      result[key] = {
        'iterations': count,
        'mean_us': watch.elapsedMicroseconds / count,
      };
    }

    await measure('version', 500, api.version);
    final items = List.generate(
      50,
      (i) => IllustSummary(
        id: i,
        title: 'Artwork $i',
        caption: 'Caption',
        userId: i,
        userName: 'Artist',
        userAccount: 'account',
        userAvatarUrl: '',
        previewUrl: '',
        originalUrl: '',
        width: 100,
        height: 100,
        pageCount: 1,
        isBookmarked: false,
        totalBookmarks: 10,
        totalViews: 100,
        tags: ['test'],
        isR18: false,
        isUgoira: false,
        pageUrls: [],
        originalPageUrls: [],
      ),
    );
    await measure(
      'records_50',
      100,
      () => api.filterArtworksByQuery(query: '', artworks: items),
    );
    final directory = await Directory.systemTemp.createTemp(
      'pixeval-native-benchmark-',
    );
    final server = await HttpServer.bind(InternetAddress.loopbackIPv4, 0);
    final payload = List<int>.filled(1024 * 1024, 42);
    server.listen((request) async {
      request.response.add(payload);
      await request.response.close();
    });
    try {
      await api.initImageCache(cacheDir: directory.path);
      final url = 'http://127.0.0.1:${server.port}/image';
      await api.fetchImage(url: url);
      await measure('cached_bytes_1mb', 30, () => api.getCachedImage(url: url));
      await api.initDownloadManager(concurrency: 1);
      final event = Completer<void>(), watch = Stopwatch();
      final subscription = api.createDownloadEventStream().listen((value) {
        if (value is DownloadProgressEvent_Completed && !event.isCompleted)
          event.complete();
      });
      await Future<void>.delayed(const Duration(milliseconds: 50));
      watch.start();
      await api.startDownload(
        url: url,
        destination: '${directory.path}/download.bin',
        overwrite: true,
      );
      await event.future.timeout(const Duration(seconds: 20));
      watch.stop();
      result['download_and_event_us'] = watch.elapsedMicroseconds;
      await subscription.cancel();
      await api.clearAllDownloads();
      result['rss_bytes'] = ProcessInfo.currentRss;
      results.add(result);
    } finally {
      await server.close(force: true);
      await api.close();
    }
  }
  final json = const JsonEncoder.withIndent('  ').convert(results);
  await File(args[1]).writeAsString('$json\n');
  stdout.writeln(json);
}
