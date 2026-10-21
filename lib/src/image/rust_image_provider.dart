import 'dart:ui' as ui;
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import '../rust/api/image.dart';

@immutable
class RustImageKey {
  final String url;
  final double scale;
  final String? referer;

  const RustImageKey({
    required this.url,
    this.scale = 1.0,
    this.referer,
  });

  @override
  bool operator ==(Object other) {
    if (identical(this, other)) return true;
    return other is RustImageKey &&
        other.url == url &&
        other.scale == scale &&
        other.referer == referer;
  }

  @override
  int get hashCode => Object.hash(url, scale, referer);

  @override
  String toString() => 'RustImageKey($url, scale: $scale)';
}

/// A high-performance [ImageProvider] backed directly by Pixeval's Rust engine
/// (`pixeval_cache` Foyer two-tier cache + `pixeval_maho` SNI bypass).
class RustImageProvider extends ImageProvider<RustImageKey> {
  final String url;
  final double scale;
  final String? referer;

  const RustImageProvider(
    this.url, {
    this.scale = 1.0,
    this.referer,
  });

  @override
  Future<RustImageKey> obtainKey(ImageConfiguration configuration) {
    return SynchronousFuture<RustImageKey>(
      RustImageKey(url: url, scale: scale, referer: referer),
    );
  }

  @override
  ImageStreamCompleter loadImage(RustImageKey key, ImageDecoderCallback decode) {
    return MultiFrameImageStreamCompleter(
      codec: _loadAsync(key, decode),
      scale: key.scale,
      debugLabel: key.url,
      informationCollector: () => <DiagnosticsNode>[
        DiagnosticsProperty<ImageProvider>('Image provider', this),
        DiagnosticsProperty<RustImageKey>('Image key', key),
      ],
    );
  }

  Future<ui.Codec> _loadAsync(RustImageKey key, ImageDecoderCallback decode) async {
    try {
      final bytes = await fetchImage(url: key.url, referer: key.referer);
      if (bytes.isEmpty) {
        PaintingBinding.instance.imageCache.evict(key);
        throw StateError('Received empty image payload from Rust bridge for: ${key.url}');
      }
      final buffer = await ui.ImmutableBuffer.fromUint8List(bytes);
      return await decode(buffer);
    } catch (e) {
      PaintingBinding.instance.imageCache.evict(key);
      rethrow;
    }
  }

  @override
  bool operator ==(Object other) {
    if (identical(this, other)) return true;
    return other is RustImageProvider &&
        other.url == url &&
        other.scale == scale &&
        other.referer == referer;
  }

  @override
  int get hashCode => Object.hash(url, scale, referer);

  @override
  String toString() => 'RustImageProvider("$url", scale: $scale)';
}
