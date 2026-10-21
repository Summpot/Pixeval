import 'dart:ui';

import 'package:flutter/material.dart';

import '../../image/rust_image_provider.dart';

/// A separate, smaller resource can decode while the main image is pending.
class BlurUpPlaceholder extends StatelessWidget {
  const BlurUpPlaceholder({
    super.key,
    required this.imageUrl,
    this.previewUrl,
    required this.fit,
  });
  final String imageUrl;
  final String? previewUrl;
  final BoxFit fit;

  @override
  Widget build(BuildContext context) {
    final preview = previewUrl;
    if (preview == null || preview.isEmpty || preview == imageUrl) {
      return ColoredBox(
        color: Theme.of(context).colorScheme.surfaceContainerHighest,
      );
    }
    return ImageFiltered(
      imageFilter: ImageFilter.blur(sigmaX: 16, sigmaY: 16),
      child: Image(
        image: RustImageProvider(preview),
        fit: fit,
        errorBuilder: (_, _, _) => const SizedBox.shrink(),
      ),
    );
  }
}

class FadeInArtwork extends StatelessWidget {
  const FadeInArtwork({
    super.key,
    required this.imageUrl,
    this.previewUrl,
    required this.fit,
    required this.image,
  });
  final String imageUrl;
  final String? previewUrl;
  final BoxFit fit;
  final Widget image;

  @override
  Widget build(BuildContext context) => Stack(
    fit: StackFit.expand,
    children: [
      BlurUpPlaceholder(imageUrl: imageUrl, previewUrl: previewUrl, fit: fit),
      TweenAnimationBuilder<double>(
        key: ValueKey(imageUrl),
        tween: Tween(begin: 0, end: 1),
        duration: const Duration(milliseconds: 250),
        builder: (_, opacity, child) => Opacity(opacity: opacity, child: child),
        child: image,
      ),
    ],
  );
}
