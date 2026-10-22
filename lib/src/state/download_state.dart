import 'dart:async';

import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../native/api/download.dart';
import '../native/api/feed.dart';
import '../native/stream_helpers.dart';

const MethodChannel _foregroundChannel = MethodChannel(
  'io.github.pixeval.pixeval_flutter/foreground',
);

void _notifyForegroundStart(String title) {
  try {
    _foregroundChannel
        .invokeMethod('startService', {'title': title})
        .catchError((_) => null);
  } catch (_) {}
}

void _notifyForegroundUpdate(String title, int progress, int max) {
  try {
    _foregroundChannel
        .invokeMethod('updateProgress', {
          'title': title,
          'progress': progress,
          'max': max,
        })
        .catchError((_) => null);
  } catch (_) {}
}

void _notifyForegroundStop() {
  try {
    _foregroundChannel.invokeMethod('stopService').catchError((_) => null);
  } catch (_) {}
}

class DownloadItem {
  final String destination;
  final String artworkId;
  final String title;
  final String previewUrl;
  final double progressPercentage;
  final BigInt downloadedBytes;
  final BigInt totalBytes;
  final String state;
  final String? errorMessage;

  const DownloadItem({
    required this.destination,
    required this.artworkId,
    required this.title,
    required this.previewUrl,
    this.progressPercentage = 0.0,
    required this.downloadedBytes,
    required this.totalBytes,
    this.state = 'Queued',
    this.errorMessage,
  });

  bool get isRunning =>
      state == 'Running' || state == 'Downloading' || state == 'Queued';
  bool get isCompleted => state == 'Completed';
  bool get isPaused => state == 'Paused';
  bool get isError => state == 'Error' || state == 'Failed';

  String get formattedSize {
    String formatBytes(BigInt bytes) {
      final bytesAsDouble = bytes.toDouble();
      if (bytesAsDouble >= 1024 * 1024 * 1024) {
        return '${(bytesAsDouble / (1024 * 1024 * 1024)).toStringAsFixed(2)} GB';
      }
      if (bytesAsDouble >= 1024 * 1024) {
        return '${(bytesAsDouble / (1024 * 1024)).toStringAsFixed(2)} MB';
      }
      if (bytesAsDouble >= 1024) {
        return '${(bytesAsDouble / 1024).toStringAsFixed(1)} KB';
      }
      return '$bytes B';
    }

    if (totalBytes > BigInt.zero) {
      return '${formatBytes(downloadedBytes)} / ${formatBytes(totalBytes)}';
    } else if (downloadedBytes > BigInt.zero) {
      return formatBytes(downloadedBytes);
    }
    return '${progressPercentage.toStringAsFixed(1)}%';
  }

  DownloadItem copyWith({
    String? destination,
    String? artworkId,
    String? title,
    String? previewUrl,
    double? progressPercentage,
    BigInt? downloadedBytes,
    BigInt? totalBytes,
    String? state,
    String? errorMessage,
  }) {
    return DownloadItem(
      destination: destination ?? this.destination,
      artworkId: artworkId ?? this.artworkId,
      title: title ?? this.title,
      previewUrl: previewUrl ?? this.previewUrl,
      progressPercentage: progressPercentage ?? this.progressPercentage,
      downloadedBytes: downloadedBytes ?? this.downloadedBytes,
      totalBytes: totalBytes ?? this.totalBytes,
      state: state ?? this.state,
      errorMessage: errorMessage ?? this.errorMessage,
    );
  }
}

class DownloadManagerState {
  final Map<String, DownloadItem> items;

  const DownloadManagerState({this.items = const {}});

  List<DownloadItem> get allItems => items.values.toList();
  List<DownloadItem> get activeItems =>
      items.values.where((item) => item.isRunning).toList();
  List<DownloadItem> get completedItems =>
      items.values.where((item) => item.isCompleted).toList();

  DownloadManagerState copyWith({Map<String, DownloadItem>? items}) {
    return DownloadManagerState(items: items ?? this.items);
  }
}

class DownloadNotifier extends Notifier<DownloadManagerState> {
  StreamSubscription<DownloadProgressEvent>? _subscription;

  @override
  DownloadManagerState build() {
    Future.microtask(() => _listenToEvents());
    ref.onDispose(() {
      _subscription?.cancel();
    });
    return const DownloadManagerState();
  }

  void _listenToEvents() {
    _subscription?.cancel();
    _subscription = getAppDownloadEventStream().listen((event) {
      event.when(
        progressUpdated:
            (destination, progressPercentage, downloadedBytes, totalBytes) {
              final current = state.items[destination];
              if (current != null) {
                final updated = current.copyWith(
                  progressPercentage: progressPercentage,
                  downloadedBytes: downloadedBytes,
                  totalBytes: totalBytes,
                  state: 'Running',
                );
                final newMap = Map<String, DownloadItem>.from(state.items);
                newMap[destination] = updated;
                state = state.copyWith(items: newMap);

                _notifyForegroundUpdate(
                  current.title,
                  progressPercentage.toInt(),
                  100,
                );
              }
            },
        stateChanged: (destination, taskState, errorMessage) {
          final current = state.items[destination];
          if (current != null) {
            final updated = current.copyWith(
              state: taskState,
              errorMessage: errorMessage,
            );
            final newMap = Map<String, DownloadItem>.from(state.items);
            newMap[destination] = updated;
            state = state.copyWith(items: newMap);
            if (state.activeItems.isEmpty) {
              _notifyForegroundStop();
            }
          }
        },
        completed: (destination) {
          final current = state.items[destination];
          if (current != null) {
            final updated = current.copyWith(
              state: 'Completed',
              progressPercentage: 100.0,
            );
            final newMap = Map<String, DownloadItem>.from(state.items);
            newMap[destination] = updated;
            state = state.copyWith(items: newMap);

            if (state.activeItems.isEmpty) {
              _notifyForegroundStop();
            }
          }
        },
      );
    });
  }

  Future<void> enqueueDownload({
    required IllustSummary illust,
    required String targetDirectory,
    int pageIndex = 0,
  }) async {
    final downloadUrls = illust.originalPageUrls.isNotEmpty
        ? illust.originalPageUrls
        : (illust.originalUrl.isNotEmpty
              ? [illust.originalUrl]
              : illust.pageUrls);

    final url = (pageIndex < downloadUrls.length)
        ? downloadUrls[pageIndex]
        : (illust.originalUrl.isNotEmpty
              ? illust.originalUrl
              : illust.previewUrl);

    final ext = url.split('.').last.split('?').first;
    final filename = '${illust.id}_p$pageIndex.$ext';
    final destination = '$targetDirectory/$filename';

    final item = DownloadItem(
      destination: destination,
      artworkId: illust.id.toString(),
      title: '${illust.title} (p$pageIndex)',
      previewUrl: illust.previewUrl,
      downloadedBytes: BigInt.zero,
      totalBytes: BigInt.zero,
    );

    final newMap = Map<String, DownloadItem>.from(state.items);
    newMap[destination] = item;
    state = state.copyWith(items: newMap);

    _notifyForegroundStart(item.title);

    await startDownload(
      url: url,
      destination: destination,
      artworkId: illust.id.toString(),
      overwrite: true,
    );
  }

  Future<void> pause(String destination) async {
    await pauseDownload(destination: destination);
    if (state.activeItems.isEmpty) {
      _notifyForegroundStop();
    }
  }

  Future<void> resume(String destination) async {
    final item = state.items[destination];
    if (item != null) {
      _notifyForegroundStart(item.title);
    }
    await resumeDownload(destination: destination);
  }

  Future<void> cancel(String destination) async {
    final success = await cancelDownload(destination: destination);
    if (success) {
      final newMap = Map<String, DownloadItem>.from(state.items);
      newMap.remove(destination);
      state = state.copyWith(items: newMap);
      if (state.activeItems.isEmpty) {
        _notifyForegroundStop();
      }
    }
  }

  void clearCompleted() {
    final newMap = Map<String, DownloadItem>.from(state.items);
    newMap.removeWhere((_, item) => item.isCompleted);
    state = state.copyWith(items: newMap);
  }
}

final downloadProvider =
    NotifierProvider<DownloadNotifier, DownloadManagerState>(
      DownloadNotifier.new,
    );
