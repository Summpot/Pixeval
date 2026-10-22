import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:extended_image/extended_image.dart';
import 'package:path_provider/path_provider.dart';

import '../constants/app_assets.dart';
import '../image/rust_image_provider.dart';
import '../native/api/feed.dart';
import '../state/download_state.dart';
import '../state/feed_state.dart';
import '../state/history_state.dart';
import 'components/ugoira_player.dart';
import 'components/blur_up.dart';
import 'download_manager_page.dart';
import 'search_page.dart';

class ArtworkDetailPage extends ConsumerStatefulWidget {
  final ValueChanged<bool>? onBookmarkChanged;
  final IllustSummary illust;
  final int initialPage;

  const ArtworkDetailPage({
    super.key,
    required this.illust,
    this.initialPage = 0,
    this.onBookmarkChanged,
  });

  @override
  ConsumerState<ArtworkDetailPage> createState() => _ArtworkDetailPageState();
}

class _ArtworkDetailPageState extends ConsumerState<ArtworkDetailPage> {
  late ExtendedPageController _pageController;
  late int _currentPage;
  late bool _isBookmarked;
  bool _showOverlay = true;

  List<String> get _pages {
    if (widget.illust.originalPageUrls.isNotEmpty) {
      return widget.illust.originalPageUrls;
    }
    if (widget.illust.originalUrl.isNotEmpty) {
      return [widget.illust.originalUrl];
    }
    if (widget.illust.pageUrls.isNotEmpty) {
      return widget.illust.pageUrls;
    }
    return [widget.illust.previewUrl];
  }

  @override
  void initState() {
    super.initState();
    _currentPage = widget.initialPage;
    _isBookmarked = widget.illust.isBookmarked;
    _pageController = ExtendedPageController(initialPage: _currentPage);

    // Record to SQLite browsing history
    Future.microtask(() {
      ref.read(browseHistoryProvider.notifier).record(widget.illust);
    });
  }

  @override
  void dispose() {
    _pageController.dispose();
    super.dispose();
  }

  void _toggleOverlay() {
    setState(() {
      _showOverlay = !_showOverlay;
    });
  }

  Future<void> _handleBookmarkToggle() async {
    HapticFeedback.lightImpact();
    final nextStatus = !_isBookmarked;
    setState(() {
      _isBookmarked = nextStatus;
    });

    try {
      final actualStatus = await toggleBookmark(
        illustId: widget.illust.id,
        isBookmarked: !_isBookmarked,
      );
      if (mounted) {
        setState(() {
          _isBookmarked = actualStatus;
        });
      }
      ref
          .read(recommendedFeedProvider.notifier)
          .updateItemBookmarkLocally(widget.illust.id, actualStatus);
      ref
          .read(rankingFeedProvider.notifier)
          .updateItemBookmarkLocally(widget.illust.id, actualStatus);
      ref
          .read(followFeedProvider.notifier)
          .updateItemBookmarkLocally(widget.illust.id, actualStatus);
      widget.onBookmarkChanged?.call(actualStatus);
    } catch (e) {
      if (mounted) {
        setState(() {
          _isBookmarked = !nextStatus;
        });
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text('收藏操作失败: $e')));
      }
    }
  }

  Future<void> _handleDownload() async {
    HapticFeedback.mediumImpact();
    final messenger = ScaffoldMessenger.of(context);
    try {
      final dir = await getApplicationDocumentsDirectory();
      final downloadDir = '${dir.path}/PixevalDownloads';
      await ref
          .read(downloadProvider.notifier)
          .enqueueDownload(
            illust: widget.illust,
            targetDirectory: downloadDir,
            pageIndex: _currentPage,
          );
      messenger.showSnackBar(
        SnackBar(
          content: Text(
            'Added to downloads: ${widget.illust.title} (p$_currentPage)',
          ),
          action: SnackBarAction(
            label: 'View',
            onPressed: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const DownloadManagerPage()),
              );
            },
          ),
        ),
      );
    } catch (e) {
      messenger.showSnackBar(SnackBar(content: Text('Download failed: $e')));
    }
  }

  void _showInfoSheet() {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Theme.of(context).colorScheme.surface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (ctx) => _ArtworkInfoBottomSheet(
        illust: widget.illust,
        isBookmarked: _isBookmarked,
        onBookmarkToggle: _handleBookmarkToggle,
        onDownload: _handleDownload,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final pages = _pages;

    return ExtendedImageSlidePage(
      slideAxis: SlideAxis.both,
      slideType: SlideType.onlyImage,
      slidePageBackgroundHandler: (Offset offset, Size pageSize) {
        final double opacity = 1.0 - (offset.distance / (pageSize.height / 2));
        return Colors.black.withValues(alpha: opacity.clamp(0.0, 1.0));
      },
      slideEndHandler:
          (
            Offset offset, {
            ExtendedImageSlidePageState? state,
            ScaleEndDetails? details,
          }) {
            return offset.distance > 100.0;
          },
      child: Scaffold(
        backgroundColor: Colors.transparent,
        body: Stack(
          children: [
            // Gallery Page View or Ugoira Player
            if (widget.illust.isUgoira)
              UgoiraPlayerWidget(
                illustId: widget.illust.id,
                previewUrl: widget.illust.previewUrl,
              )
            else
              GestureDetector(
                onTap: _toggleOverlay,
                child: ExtendedImageGesturePageView.builder(
                  controller: _pageController,
                  itemCount: pages.length,
                  onPageChanged: (index) {
                    setState(() {
                      _currentPage = index;
                    });
                  },
                  itemBuilder: (context, index) {
                    final url = pages[index];
                    final imageWidget = ExtendedImage(
                      image: RustImageProvider(url),
                      fit: BoxFit.contain,
                      mode: ExtendedImageMode.gesture,
                      enableSlideOutPage: true,
                      initGestureConfigHandler: (state) {
                        return GestureConfig(
                          minScale: 0.9,
                          animationMinScale: 0.7,
                          maxScale: 4.0,
                          animationMaxScale: 4.5,
                          speed: 1.0,
                          inertialSpeed: 100.0,
                          inPageView: true,
                          initialScale: 1.0,
                          initialAlignment: InitialAlignment.center,
                        );
                      },
                      loadStateChanged: (state) {
                        switch (state.extendedImageLoadState) {
                          case LoadState.loading:
                            final preview =
                                index < widget.illust.pageUrls.length
                                ? widget.illust.pageUrls[index]
                                : widget.illust.previewUrl;
                            if (preview.isNotEmpty && preview != url) {
                              return BlurUpPlaceholder(
                                imageUrl: url,
                                previewUrl: preview,
                                fit: BoxFit.contain,
                              );
                            }
                            return Center(
                              child: SizedBox(
                                width: 32,
                                height: 32,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2.5,
                                  value:
                                      state
                                              .loadingProgress
                                              ?.expectedTotalBytes !=
                                          null
                                      ? (state
                                                .loadingProgress!
                                                .cumulativeBytesLoaded /
                                            state
                                                .loadingProgress!
                                                .expectedTotalBytes!)
                                      : null,
                                ),
                              ),
                            );
                          case LoadState.failed:
                            return Center(
                              child: Column(
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  Image.asset(
                                    AppAssets.imageNotAvailable,
                                    width: 64,
                                    height: 64,
                                    errorBuilder: (_, _, _) => const Icon(
                                      Icons.broken_image_outlined,
                                      size: 48,
                                      color: Colors.grey,
                                    ),
                                  ),
                                  const SizedBox(height: 8),
                                  Text(
                                    '图片加载失败',
                                    style: TextStyle(
                                      color: theme.colorScheme.onSurface
                                          .withValues(alpha: 0.7),
                                    ),
                                  ),
                                  TextButton(
                                    onPressed: () => state.reLoadImage(),
                                    child: const Text('重试'),
                                  ),
                                ],
                              ),
                            );
                          case LoadState.completed:
                            return FadeInArtwork(
                              imageUrl: url,
                              previewUrl: index < widget.illust.pageUrls.length
                                  ? widget.illust.pageUrls[index]
                                  : widget.illust.previewUrl,
                              fit: BoxFit.contain,
                              image: state.completedWidget,
                            );
                        }
                      },
                    );

                    if (index == 0) {
                      return Hero(
                        tag: 'artwork_image_${widget.illust.id}',
                        child: imageWidget,
                      );
                    }
                    return imageWidget;
                  },
                ),
              ),

            // Top Bar Overlay
            AnimatedPositioned(
              duration: const Duration(milliseconds: 200),
              top: _showOverlay ? 0 : -100,
              left: 0,
              right: 0,
              child: Container(
                padding: EdgeInsets.only(
                  top: MediaQuery.of(context).padding.top + 8,
                  left: 8,
                  right: 8,
                  bottom: 8,
                ),
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topCenter,
                    end: Alignment.bottomCenter,
                    colors: [
                      Colors.black.withValues(alpha: 0.7),
                      Colors.transparent,
                    ],
                  ),
                ),
                child: Row(
                  children: [
                    IconButton(
                      icon: const Icon(Icons.arrow_back, color: Colors.white),
                      onPressed: () => Navigator.of(context).pop(),
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        widget.illust.title,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          color: Colors.white,
                          fontSize: 16,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                    if (pages.length > 1)
                      Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 10,
                          vertical: 4,
                        ),
                        decoration: BoxDecoration(
                          color: Colors.black.withValues(alpha: 0.5),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Text(
                          '${_currentPage + 1} / ${pages.length}',
                          style: const TextStyle(
                            color: Colors.white,
                            fontSize: 13,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                    IconButton(
                      icon: const Icon(Icons.download, color: Colors.white),
                      tooltip: 'Download',
                      onPressed: _handleDownload,
                    ),
                    IconButton(
                      icon: Icon(
                        _isBookmarked ? Icons.favorite : Icons.favorite_border,
                        color: _isBookmarked ? Colors.redAccent : Colors.white,
                      ),
                      onPressed: _handleBookmarkToggle,
                    ),
                  ],
                ),
              ),
            ),

            // Bottom Bar Overlay
            AnimatedPositioned(
              duration: const Duration(milliseconds: 200),
              bottom: _showOverlay ? 0 : -140,
              left: 0,
              right: 0,
              child: Container(
                padding: EdgeInsets.only(
                  left: 16,
                  right: 16,
                  top: 12,
                  bottom: MediaQuery.of(context).padding.bottom + 12,
                ),
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.bottomCenter,
                    end: Alignment.topCenter,
                    colors: [
                      Colors.black.withValues(alpha: 0.8),
                      Colors.transparent,
                    ],
                  ),
                ),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    // Filmstrip thumbnail scroller for multi-page illustrations
                    if (pages.length > 1)
                      SizedBox(
                        height: 52,
                        child: ListView.separated(
                          scrollDirection: Axis.horizontal,
                          itemCount: pages.length,
                          separatorBuilder: (_, _) => const SizedBox(width: 8),
                          itemBuilder: (context, index) {
                            final isSelected = index == _currentPage;
                            return GestureDetector(
                              onTap: () {
                                _pageController.jumpToPage(index);
                              },
                              child: Container(
                                width: 52,
                                decoration: BoxDecoration(
                                  borderRadius: BorderRadius.circular(8),
                                  border: Border.all(
                                    color: isSelected
                                        ? theme.colorScheme.primary
                                        : Colors.white.withValues(alpha: 0.4),
                                    width: isSelected ? 2.5 : 1,
                                  ),
                                ),
                                clipBehavior: Clip.antiAlias,
                                child: ExtendedImage(
                                  image: RustImageProvider(pages[index]),
                                  fit: BoxFit.cover,
                                ),
                              ),
                            );
                          },
                        ),
                      ),
                    if (pages.length > 1) const SizedBox(height: 10),
                    // Quick Action & Author summary bar
                    Row(
                      children: [
                        CircleAvatar(
                          radius: 18,
                          backgroundImage:
                              widget.illust.userAvatarUrl.isNotEmpty
                              ? RustImageProvider(widget.illust.userAvatarUrl)
                              : null,
                          child: widget.illust.userAvatarUrl.isEmpty
                              ? const Icon(Icons.person, size: 20)
                              : null,
                        ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Text(
                                widget.illust.userName,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(
                                  color: Colors.white,
                                  fontSize: 14,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                              Text(
                                '@${widget.illust.userAccount}',
                                style: TextStyle(
                                  color: Colors.white.withValues(alpha: 0.7),
                                  fontSize: 11,
                                ),
                              ),
                            ],
                          ),
                        ),
                        FilledButton.tonalIcon(
                          style: FilledButton.styleFrom(
                            backgroundColor: Colors.white.withValues(
                              alpha: 0.2,
                            ),
                            foregroundColor: Colors.white,
                          ),
                          onPressed: _showInfoSheet,
                          icon: const Icon(Icons.info_outline, size: 18),
                          label: const Text('详情'),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ArtworkInfoBottomSheet extends StatelessWidget {
  final IllustSummary illust;
  final bool isBookmarked;
  final VoidCallback onBookmarkToggle;
  final VoidCallback onDownload;

  const _ArtworkInfoBottomSheet({
    required this.illust,
    required this.isBookmarked,
    required this.onBookmarkToggle,
    required this.onDownload,
  });

  String _cleanCaption(String html) {
    return html
        .replaceAll(RegExp(r'<br\s*/?>', caseSensitive: false), '\n')
        .replaceAll(RegExp(r'<[^>]*>'), '')
        .trim();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final cleanedCaption = _cleanCaption(illust.caption);

    return DraggableScrollableSheet(
      initialChildSize: 0.55,
      minChildSize: 0.35,
      maxChildSize: 0.9,
      expand: false,
      builder: (context, scrollController) {
        return SingleChildScrollView(
          controller: scrollController,
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Drag handle
              Center(
                child: Container(
                  width: 40,
                  height: 4,
                  decoration: BoxDecoration(
                    color: theme.colorScheme.onSurfaceVariant.withValues(
                      alpha: 0.4,
                    ),
                    borderRadius: BorderRadius.circular(2),
                  ),
                ),
              ),
              const SizedBox(height: 16),

              // Title
              Text(
                illust.title,
                style: theme.textTheme.titleLarge?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
              ),
              const SizedBox(height: 12),

              // Author info row
              Row(
                children: [
                  CircleAvatar(
                    radius: 22,
                    backgroundImage: illust.userAvatarUrl.isNotEmpty
                        ? RustImageProvider(illust.userAvatarUrl)
                        : null,
                    child: illust.userAvatarUrl.isEmpty
                        ? const Icon(Icons.person)
                        : null,
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          illust.userName,
                          style: theme.textTheme.bodyLarge?.copyWith(
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                        Text(
                          'ID: ${illust.userId} · @${illust.userAccount}',
                          style: theme.textTheme.bodySmall?.copyWith(
                            color: theme.colorScheme.onSurfaceVariant,
                          ),
                        ),
                      ],
                    ),
                  ),
                  IconButton.filledTonal(
                    icon: const Icon(Icons.download),
                    tooltip: 'Download',
                    onPressed: onDownload,
                  ),
                  const SizedBox(width: 8),
                  IconButton.filledTonal(
                    icon: Icon(
                      isBookmarked ? Icons.favorite : Icons.favorite_border,
                      color: isBookmarked ? Colors.redAccent : null,
                    ),
                    onPressed: onBookmarkToggle,
                  ),
                ],
              ),
              const Divider(height: 28),

              // Metrics & Resolution row
              Wrap(
                spacing: 12,
                runSpacing: 8,
                children: [
                  _MetricBadge(
                    icon: Icons.favorite,
                    iconColor: Colors.redAccent,
                    label: '${illust.totalBookmarks} 收藏',
                  ),
                  _MetricBadge(
                    icon: Icons.visibility,
                    iconColor: theme.colorScheme.primary,
                    label: '${illust.totalViews} 浏览',
                  ),
                  _MetricBadge(
                    icon: Icons.photo_size_select_actual_outlined,
                    label: '${illust.width} × ${illust.height}',
                  ),
                  if (illust.pageCount > 1)
                    _MetricBadge(
                      icon: Icons.auto_stories_outlined,
                      label: '${illust.pageCount} 页',
                    ),
                  if (illust.isR18)
                    const _MetricBadge(
                      icon: Icons.explicit,
                      iconColor: Colors.deepOrange,
                      label: 'R-18',
                    ),
                ],
              ),
              const SizedBox(height: 16),

              // Tags
              if (illust.tags.isNotEmpty) ...[
                Text(
                  '标签',
                  style: theme.textTheme.labelLarge?.copyWith(
                    fontWeight: FontWeight.bold,
                    color: theme.colorScheme.primary,
                  ),
                ),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 6,
                  runSpacing: 6,
                  children: illust.tags.map((tag) {
                    return ActionChip(
                      label: Text('#$tag'),
                      labelStyle: const TextStyle(fontSize: 12),
                      materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                      padding: const EdgeInsets.symmetric(horizontal: 4),
                      onPressed: () {
                        Navigator.of(context).push(
                          MaterialPageRoute(
                            builder: (_) => SearchPage(initialQuery: tag),
                          ),
                        );
                      },
                    );
                  }).toList(),
                ),
                const SizedBox(height: 16),
              ],

              // Caption / Description
              if (cleanedCaption.isNotEmpty) ...[
                Text(
                  '描述',
                  style: theme.textTheme.labelLarge?.copyWith(
                    fontWeight: FontWeight.bold,
                    color: theme.colorScheme.primary,
                  ),
                ),
                const SizedBox(height: 8),
                SelectableText(
                  cleanedCaption,
                  style: theme.textTheme.bodyMedium?.copyWith(height: 1.5),
                ),
                const SizedBox(height: 16),
              ],

              // Meta ID info
              Text(
                '插画 ID: ${illust.id}',
                style: theme.textTheme.bodySmall?.copyWith(
                  color: theme.colorScheme.outline,
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}

class _MetricBadge extends StatelessWidget {
  final IconData icon;
  final Color? iconColor;
  final String label;

  const _MetricBadge({required this.icon, this.iconColor, required this.label});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: theme.colorScheme.surfaceContainerHighest.withValues(alpha: 0.5),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(
            icon,
            size: 14,
            color: iconColor ?? theme.colorScheme.onSurfaceVariant,
          ),
          const SizedBox(width: 6),
          Text(
            label,
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w500,
              color: theme.colorScheme.onSurfaceVariant,
            ),
          ),
        ],
      ),
    );
  }
}
