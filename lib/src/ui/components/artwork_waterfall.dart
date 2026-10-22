import 'package:flutter/material.dart';
import 'package:waterfall_flow/waterfall_flow.dart';
import 'package:easy_refresh/easy_refresh.dart';

import '../../native/api/feed.dart';
import 'artwork_card.dart';

class ArtworkWaterfall extends StatelessWidget {
  final List<IllustSummary> items;
  final bool isLoading;
  final bool isRefreshing;
  final String? errorMessage;
  final Future<void> Function() onRefresh;
  final Future<void> Function() onLoadMore;
  final void Function(int illustId, bool isBookmarked)? onBookmarkToggle;
  final void Function(int illustId, bool isBookmarked)? onBookmarkChanged;

  const ArtworkWaterfall({
    super.key,
    required this.items,
    this.isLoading = false,
    this.isRefreshing = false,
    this.errorMessage,
    required this.onRefresh,
    required this.onLoadMore,
    this.onBookmarkToggle,
    this.onBookmarkChanged,
  });

  @override
  Widget build(BuildContext context) {
    if (errorMessage != null && items.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24.0),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(
                Icons.cloud_off_outlined,
                size: 48,
                color: Colors.grey,
              ),
              const SizedBox(height: 12),
              Text(
                'Failed to load artworks',
                style: Theme.of(context).textTheme.titleMedium,
              ),
              const SizedBox(height: 6),
              Text(
                errorMessage!,
                textAlign: TextAlign.center,
                style: const TextStyle(color: Colors.grey, fontSize: 13),
              ),
              const SizedBox(height: 16),
              FilledButton.icon(
                onPressed: onRefresh,
                icon: const Icon(Icons.refresh),
                label: const Text('Retry'),
              ),
            ],
          ),
        ),
      );
    }

    if (isLoading && items.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }

    final screenWidth = MediaQuery.of(context).size.width;
    final int crossAxisCount = (screenWidth / 180).floor().clamp(2, 6);

    return EasyRefresh(
      onRefresh: onRefresh,
      onLoad: onLoadMore,
      child: WaterfallFlow.builder(
        padding: const EdgeInsets.all(8),
        gridDelegate: SliverWaterfallFlowDelegateWithFixedCrossAxisCount(
          crossAxisCount: crossAxisCount,
          crossAxisSpacing: 8,
          mainAxisSpacing: 8,
        ),
        itemCount: items.length,
        itemBuilder: (context, index) {
          final item = items[index];
          return ArtworkCard(
            illust: item,
            onBookmarkChanged: (value) =>
                onBookmarkChanged?.call(item.id, value),
            onBookmarkToggle: () {
              onBookmarkToggle?.call(item.id, item.isBookmarked);
            },
          );
        },
      ),
    );
  }
}
