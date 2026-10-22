import '../pixeval.g.dart';
export '../pixeval.g.dart' show DownloadProgressEvent;

import '../native_api.dart';

Stream<DownloadProgressEvent> createDownloadEventStream({String? streamId}) =>
    NativeApi.instance.api.createDownloadEventStream(streamId: streamId);

Future<void> cancelDownloadEventStream({String? streamId}) =>
    NativeApi.instance.api.cancelDownloadEventStream(streamId: streamId);

Future<void> initDownloadManager({required int concurrency}) =>
    NativeApi.instance.api.initDownloadManager(concurrency: concurrency);

Future<void> startDownload({
  required String url,
  required String destination,
  String? artworkId,
  required bool overwrite,
}) => NativeApi.instance.api.startDownload(
  url: url,
  destination: destination,
  artworkId: artworkId,
  overwrite: overwrite,
);

Future<bool> pauseDownload({required String destination}) =>
    NativeApi.instance.api.pauseDownload(destination: destination);

Future<bool> resumeDownload({required String destination}) =>
    NativeApi.instance.api.resumeDownload(destination: destination);

Future<bool> cancelDownload({required String destination}) =>
    NativeApi.instance.api.cancelDownload(destination: destination);

Future<bool> removeDownload({required String destination}) =>
    NativeApi.instance.api.removeDownload(destination: destination);

Future<void> clearAllDownloads() => NativeApi.instance.api.clearAllDownloads();
