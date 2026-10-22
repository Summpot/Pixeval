import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter/widgets.dart';
import 'package:pixeval_flutter/src/native/native_api.dart';
import 'package:pixeval_flutter/src/native/models.dart';
import 'package:pixeval_flutter/src/native/pixeval.g.dart';
import 'package:pixeval_flutter/src/native/service.dart';

void check(bool condition, String message) {
  if (!condition) throw StateError(message);
}

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  final results = <Map<String, Object>>[];
  try {
    for (final engine in NativeEngine.values) {
      final directory = await Directory.systemTemp.createTemp(
        'pixeval-native-${engine.name}-',
      );
      await NativeApi.init(engine: engine);
      final api = NativeApi.instance.api;
      check((await api.version()).isNotEmpty, 'version');
      check(
        (await api.createPkceAuthParams()).codeVerifier.length == 43,
        'PKCE',
      );
      await api.initStorage(dbPath: '${directory.path}/test.db');
      await api.addSearchHistory(keyword: 'native-smoke', translatedName: '测试');
      check(
        (await api.getSearchHistory(limit: 10)).single.value == 'native-smoke',
        'SQLite roundtrip',
      );
      final config = await api.getNetworkConfig();
      await api.updateNetworkConfig(config: config);
      const image = IllustSummary(
        id: 42,
        title: 'Native smoke',
        caption: '',
        userId: 1,
        userName: 'Artist',
        userAccount: 'artist',
        userAvatarUrl: '',
        previewUrl: '',
        originalUrl: '',
        width: 640,
        height: 480,
        pageCount: 1,
        isBookmarked: false,
        totalBookmarks: 10,
        totalViews: 100,
        tags: ['test'],
        isR18: false,
        isUgoira: false,
        pageUrls: [],
        originalPageUrls: [],
      );
      check(
        (await api.filterArtworksByQuery(
              query: '#test',
              artworks: [image],
            )).length ==
            1,
        'cross-crate filter types',
      );
      check(
        (await api.filterArtworksByQuery(
          query: '#missing',
          artworks: [image],
        )).isEmpty,
        'filter false',
      );
      var rejected = false;
      try {
        await api.filterArtworksByQuery(query: '(tag:test', artworks: [image]);
      } catch (_) {
        rejected = true;
      }
      check(rejected, 'fallible async error');
      check(
        await api.parsePixivUrl(url: 'https://www.pixiv.net/artworks/42') ==
            const ParsedPixivLink.artwork(id: 42),
        'tagged enum',
      );
      final bindings = (api as PixevalService).bindings;
      final feed = bindings.appIllustrationFeedRecommended();
      feed.cancel();
      await feed.dispose();
      final session = api
          .createSessionEventStream(streamId: 'smoke-session')
          .listen((_) {}, onError: (Object e) => throw e);
      final server = await HttpServer.bind(InternetAddress.loopbackIPv4, 0);
      server.listen((request) async {
        request.response.add(List<int>.filled(8192, 42));
        await request.response.close();
      });
      final destination = '${directory.path}/download.bin';
      final completed = Completer<void>();
      final subscription = api
          .createDownloadEventStream(streamId: 'smoke-download')
          .listen((event) {
            if (event is DownloadProgressEvent_Completed &&
                event.destination == destination &&
                !completed.isCompleted)
              completed.complete();
          }, onError: completed.completeError);
      try {
        await api.initDownloadManager(concurrency: 2);
        await Future<void>.delayed(const Duration(milliseconds: 100));
        await api.startDownload(
          url: 'http://127.0.0.1:${server.port}/file',
          destination: destination,
          overwrite: true,
        );
        await completed.future.timeout(const Duration(seconds: 20));
        check(await File(destination).length() == 8192, 'download bytes');
        await api.initImageCache(cacheDir: '${directory.path}/cache');
        final url = 'http://127.0.0.1:${server.port}/image';
        check((await api.fetchImage(url: url)).length == 8192, 'image fetch');
        check(
          (await api.getCachedImage(url: url))?.length == 8192,
          'owned cached bytes',
        );
      } finally {
        await api.clearAllDownloads();
        await api.cancelDownloadEventStream(streamId: 'smoke-download');
        await subscription.cancel();
        await api.cancelSessionEventStream(streamId: 'smoke-session');
        await session.cancel();
        await server.close(force: true);
        await NativeApi.dispose();
      }
      results.add({'engine': engine.name, 'passed': true});
    }
    final result = jsonEncode({
      'passed': true,
      'platform': Platform.operatingSystem,
      'results': results,
    });
    // The native embedding forwards this line to stdout / Android logcat.
    debugPrint('PIXEVAL_NATIVE_SMOKE_RESULT $result');
    runApp(
      const Directionality(
        textDirection: TextDirection.ltr,
        child: Center(child: Text('Native smoke passed')),
      ),
    );
  } catch (error, stack) {
    debugPrint(
      'PIXEVAL_NATIVE_SMOKE_RESULT ${jsonEncode({'passed': false, 'results': results, 'error': error.toString()})}',
    );
    debugPrintStack(stackTrace: stack);
    runApp(
      const Directionality(
        textDirection: TextDirection.ltr,
        child: Center(child: Text('Native smoke failed')),
      ),
    );
  }
}
