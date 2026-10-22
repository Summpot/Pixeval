import 'dart:typed_data';

import '../pixeval.g.dart';
export '../pixeval.g.dart' show CacheStatsDto;

import '../native_api.dart';

Future<void> initImageCache({
  required String cacheDir,
  BigInt? capacityBytes,
}) => NativeApi.instance.api.initImageCache(
  cacheDir: cacheDir,
  capacityBytes: capacityBytes,
);

Future<Uint8List> fetchImage({required String url, String? referer}) =>
    NativeApi.instance.api.fetchImage(url: url, referer: referer);

Future<Uint8List?> getCachedImage({required String url}) =>
    NativeApi.instance.api.getCachedImage(url: url);

Future<void> clearImageCache() => NativeApi.instance.api.clearImageCache();

Future<CacheStatsDto> getCacheStats() => NativeApi.instance.api.getCacheStats();
