import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../rust/api/image.dart';

class CacheState {
  final int entryCount;
  final BigInt totalAllocatedBytes;
  final BigInt totalUsedBytes;
  final bool isLoading;

  const CacheState({
    this.entryCount = 0,
    required this.totalAllocatedBytes,
    required this.totalUsedBytes,
    this.isLoading = false,
  });

  String get formattedUsedSize {
    final bytes = totalUsedBytes.toInt();
    if (bytes < 1024) return '$bytes B';
    if (bytes < 1024 * 1024) return '${(bytes / 1024).toStringAsFixed(1)} KB';
    if (bytes < 1024 * 1024 * 1024) {
      return '${(bytes / (1024 * 1024)).toStringAsFixed(1)} MB';
    }
    return '${(bytes / (1024 * 1024 * 1024)).toStringAsFixed(2)} GB';
  }

  CacheState copyWith({
    int? entryCount,
    BigInt? totalAllocatedBytes,
    BigInt? totalUsedBytes,
    bool? isLoading,
  }) {
    return CacheState(
      entryCount: entryCount ?? this.entryCount,
      totalAllocatedBytes: totalAllocatedBytes ?? this.totalAllocatedBytes,
      totalUsedBytes: totalUsedBytes ?? this.totalUsedBytes,
      isLoading: isLoading ?? this.isLoading,
    );
  }
}

class CacheNotifier extends Notifier<CacheState> {
  @override
  CacheState build() {
    Future.microtask(() => refreshStats());
    return CacheState(
      totalAllocatedBytes: BigInt.zero,
      totalUsedBytes: BigInt.zero,
    );
  }

  Future<void> refreshStats() async {
    try {
      final stats = await getCacheStats();
      state = state.copyWith(
        entryCount: stats.entryCount,
        totalAllocatedBytes: stats.totalAllocatedBytes,
        totalUsedBytes: stats.totalUsedBytes,
      );
    } catch (_) {}
  }

  Future<void> clearCache() async {
    state = state.copyWith(isLoading: true);
    try {
      await clearImageCache();
      await refreshStats();
    } finally {
      state = state.copyWith(isLoading: false);
    }
  }
}

final cacheProvider =
    NotifierProvider<CacheNotifier, CacheState>(CacheNotifier.new);
