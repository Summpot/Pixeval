import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:extended_image/extended_image.dart';

import '../../image/rust_image_provider.dart';
import '../../rust/api/feed.dart';
import '../artwork_detail_page.dart';
import 'blur_up.dart';

class ArtworkCard extends StatelessWidget {
  final IllustSummary illust;
  final VoidCallback? onBookmarkToggle;
  final ValueChanged<bool>? onBookmarkChanged;

  const ArtworkCard({
    super.key,
    required this.illust,
    this.onBookmarkToggle,
    this.onBookmarkChanged,
  });

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final aspectRatio = (illust.width > 0 && illust.height > 0)
        ? (illust.width / illust.height).clamp(0.5, 2.0)
        : 1.0;

    return Card(
      elevation: 0,
      clipBehavior: Clip.antiAlias,
      color: theme.colorScheme.surfaceContainerLow,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      child: InkWell(
        onTap: () {
          HapticFeedback.selectionClick();
          Navigator.of(context).push(
            MaterialPageRoute(
              builder: (_) => ArtworkDetailPage(
                illust: illust,
                onBookmarkChanged: onBookmarkChanged,
              ),
            ),
          );
        },
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            AspectRatio(
              aspectRatio: aspectRatio,
              child: Stack(
                fit: StackFit.expand,
                children: [
                  Hero(
                    tag: 'artwork_image_${illust.id}',
                    child: ExtendedImage(
                      image: RustImageProvider(illust.previewUrl),
                      fit: BoxFit.cover,
                      enableLoadState: true,
                      loadStateChanged: (state) {
                        switch (state.extendedImageLoadState) {
                          case LoadState.loading:
                            // Progressive blur-up placeholder using low-res preview
                            return Stack(
                              fit: StackFit.expand,
                              children: [
                                BlurUpPlaceholder(
                                  imageUrl: illust.previewUrl,
                                  previewUrl: illust.thumbnailUrl,
                                  fit: BoxFit.cover,
                                ),
                                Center(
                                  child: SizedBox(
                                    width: 20,
                                    height: 20,
                                    child: CircularProgressIndicator(
                                      strokeWidth: 2,
                                      valueColor: AlwaysStoppedAnimation<Color>(
                                        theme.colorScheme.primary.withValues(
                                          alpha: 0.7,
                                        ),
                                      ),
                                    ),
                                  ),
                                ),
                              ],
                            );
                          case LoadState.failed:
                            return Container(
                              color: Colors.grey.withValues(alpha: 0.15),
                              child: const Center(
                                child: Icon(
                                  Icons.broken_image_outlined,
                                  color: Colors.grey,
                                ),
                              ),
                            );
                          case LoadState.completed:
                            return FadeInArtwork(
                              imageUrl: illust.previewUrl,
                              previewUrl: illust.thumbnailUrl,
                              fit: BoxFit.cover,
                              image: state.completedWidget,
                            );
                        }
                      },
                    ),
                  ),
                  // Badges: Multi-page, R-18, Ugoira
                  Positioned(
                    top: 8,
                    right: 8,
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        if (illust.isR18)
                          Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 5,
                              vertical: 2,
                            ),
                            margin: const EdgeInsets.only(right: 4),
                            decoration: BoxDecoration(
                              color: Colors.red[800]?.withValues(alpha: 0.9),
                              borderRadius: BorderRadius.circular(4),
                            ),
                            child: const Text(
                              'R-18',
                              style: TextStyle(
                                color: Colors.white,
                                fontSize: 10,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ),
                        if (illust.isUgoira)
                          Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 5,
                              vertical: 2,
                            ),
                            margin: const EdgeInsets.only(right: 4),
                            decoration: BoxDecoration(
                              color: Colors.blue[700]?.withValues(alpha: 0.9),
                              borderRadius: BorderRadius.circular(4),
                            ),
                            child: const Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                Icon(
                                  Icons.play_arrow,
                                  size: 10,
                                  color: Colors.white,
                                ),
                                SizedBox(width: 2),
                                Text(
                                  '动图',
                                  style: TextStyle(
                                    color: Colors.white,
                                    fontSize: 10,
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        if (illust.pageCount > 1)
                          Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 5,
                              vertical: 2,
                            ),
                            decoration: BoxDecoration(
                              color: Colors.black.withValues(alpha: 0.65),
                              borderRadius: BorderRadius.circular(4),
                            ),
                            child: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                const Icon(
                                  Icons.photo_library,
                                  size: 10,
                                  color: Colors.white,
                                ),
                                const SizedBox(width: 3),
                                Text(
                                  '${illust.pageCount}',
                                  style: const TextStyle(
                                    color: Colors.white,
                                    fontSize: 10,
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                              ],
                            ),
                          ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(10.0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    illust.title,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  const SizedBox(height: 6),
                  Row(
                    children: [
                      if (illust.userAvatarUrl.isNotEmpty)
                        ClipOval(
                          child: ExtendedImage(
                            image: RustImageProvider(illust.userAvatarUrl),
                            width: 18,
                            height: 18,
                            fit: BoxFit.cover,
                            loadStateChanged: (s) {
                              if (s.extendedImageLoadState ==
                                  LoadState.failed) {
                                return const Icon(
                                  Icons.person,
                                  size: 16,
                                  color: Colors.grey,
                                );
                              }
                              return null;
                            },
                          ),
                        )
                      else
                        const Icon(Icons.person, size: 16, color: Colors.grey),
                      const SizedBox(width: 6),
                      Expanded(
                        child: Text(
                          illust.userName,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: TextStyle(
                            fontSize: 11,
                            color: theme.textTheme.bodySmall?.color?.withValues(
                              alpha: 0.75,
                            ),
                          ),
                        ),
                      ),
                      IconButton(
                        visualDensity: VisualDensity.compact,
                        padding: EdgeInsets.zero,
                        constraints: const BoxConstraints(),
                        icon: Icon(
                          illust.isBookmarked
                              ? Icons.favorite
                              : Icons.favorite_border,
                          size: 18,
                          color: illust.isBookmarked ? Colors.red : Colors.grey,
                        ),
                        onPressed: onBookmarkToggle,
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
