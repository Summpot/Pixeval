import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:extended_image/extended_image.dart';
import '../image/rust_image_provider.dart';
import '../state/auth_state.dart';
import '../state/cache_state.dart';
import '../state/feed_state.dart';
import 'components/artwork_waterfall.dart';
import 'download_manager_page.dart';
import 'history_page.dart';
import 'login_page.dart';
import 'network_settings_page.dart';
import 'search_page.dart';

class HomeShell extends ConsumerStatefulWidget {
  const HomeShell({super.key});

  @override
  ConsumerState<HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends ConsumerState<HomeShell> {
  int _currentIndex = 0;

  final _pages = const [
    ExploreTab(),
    RankingsTab(),
    FollowFeedsTab(),
    ProfileTab(),
  ];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: IndexedStack(
        index: _currentIndex,
        children: _pages,
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _currentIndex,
        onDestinationSelected: (idx) {
          setState(() => _currentIndex = idx);
        },
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.explore_outlined),
            selectedIcon: Icon(Icons.explore),
            label: 'Explore',
          ),
          NavigationDestination(
            icon: Icon(Icons.leaderboard_outlined),
            selectedIcon: Icon(Icons.leaderboard),
            label: 'Rankings',
          ),
          NavigationDestination(
            icon: Icon(Icons.dynamic_feed_outlined),
            selectedIcon: Icon(Icons.dynamic_feed),
            label: 'Feeds',
          ),
          NavigationDestination(
            icon: Icon(Icons.person_outline),
            selectedIcon: Icon(Icons.person),
            label: 'Profile',
          ),
        ],
      ),
    );
  }
}

class ExploreTab extends ConsumerWidget {
  const ExploreTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final feed = ref.watch(recommendedFeedProvider);
    final notifier = ref.read(recommendedFeedProvider.notifier);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Discovery'),
        actions: [
          IconButton(
            icon: const Icon(Icons.search),
            onPressed: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const SearchPage()),
              );
            },
          ),
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () => notifier.refresh(),
          ),
        ],
      ),
      body: ArtworkWaterfall(
        items: feed.items,
        isLoading: feed.isLoading,
        isRefreshing: feed.isRefreshing,
        errorMessage: feed.errorMessage,
        onRefresh: () => notifier.refresh(),
        onLoadMore: () => notifier.loadMore(),
        onBookmarkToggle: (id, bookmarked) => notifier.toggleItemBookmark(id, bookmarked),
      ),
    );
  }
}

class RankingsTab extends ConsumerStatefulWidget {
  const RankingsTab({super.key});

  @override
  ConsumerState<RankingsTab> createState() => _RankingsTabState();
}

class _RankingsTabState extends ConsumerState<RankingsTab> {
  static const _modes = [
    ('day', 'Daily'),
    ('week', 'Weekly'),
    ('month', 'Monthly'),
    ('rookie', 'Rookie'),
    ('day_male', 'Male'),
    ('day_female', 'Female'),
  ];

  @override
  Widget build(BuildContext context) {
    final feed = ref.watch(rankingFeedProvider);
    final notifier = ref.read(rankingFeedProvider.notifier);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Rankings'),
        actions: [
          IconButton(
            icon: const Icon(Icons.search),
            onPressed: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const SearchPage()),
              );
            },
          ),
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () => notifier.refresh(),
          ),
        ],
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(48),
          child: SizedBox(
            height: 48,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
              itemCount: _modes.length,
              separatorBuilder: (_, _) => const SizedBox(width: 8),
              itemBuilder: (context, index) {
                final mode = _modes[index];
                final isSelected = notifier.mode == mode.$1;
                return ChoiceChip(
                  label: Text(mode.$2),
                  selected: isSelected,
                  onSelected: (selected) {
                    if (selected) {
                      notifier.setMode(mode.$1);
                    }
                  },
                );
              },
            ),
          ),
        ),
      ),
      body: ArtworkWaterfall(
        items: feed.items,
        isLoading: feed.isLoading,
        isRefreshing: feed.isRefreshing,
        errorMessage: feed.errorMessage,
        onRefresh: () => notifier.refresh(),
        onLoadMore: () => notifier.loadMore(),
        onBookmarkToggle: (id, bookmarked) => notifier.toggleItemBookmark(id, bookmarked),
      ),
    );
  }
}

class ProfileTab extends ConsumerWidget {
  const ProfileTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final authState = ref.watch(authProvider);
    final cacheState = ref.watch(cacheProvider);
    final user = authState.user;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Profile & Settings'),
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Card(
            elevation: 1,
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  CircleAvatar(
                    radius: 30,
                    backgroundColor: const Color(0xFF0096FA).withValues(alpha: 0.1),
                    child: user != null && user.avatarUrl.isNotEmpty
                        ? ClipOval(
                            child: ExtendedImage(
                              image: RustImageProvider(user.avatarUrl),
                              width: 60,
                              height: 60,
                              fit: BoxFit.cover,
                            ),
                          )
                        : const Icon(Icons.person, size: 36, color: Color(0xFF0096FA)),
                  ),
                  const SizedBox(width: 16),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          user?.name ?? 'Not Logged In',
                          style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 18),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          user != null ? '@${user.account}' : 'Sign in to sync your Pixiv content',
                          style: TextStyle(color: Colors.grey[600], fontSize: 13),
                        ),
                        if (user != null && user.isPremium) ...[
                          const SizedBox(height: 4),
                          Container(
                            padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                            decoration: BoxDecoration(
                              color: Colors.amber.withValues(alpha: 0.2),
                              borderRadius: BorderRadius.circular(4),
                            ),
                            child: const Text(
                              'Pixiv Premium',
                              style: TextStyle(
                                color: Colors.amber,
                                fontSize: 11,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),
          Card(
            elevation: 1,
            child: Column(
              children: [
                ListTile(
                  leading: const Icon(Icons.download_outlined),
                  title: const Text('Download Manager'),
                  subtitle: const Text('View and manage background downloads'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () {
                    Navigator.of(context).push(
                      MaterialPageRoute(
                        builder: (_) => const DownloadManagerPage(),
                      ),
                    );
                  },
                ),
                const Divider(height: 1),
                ListTile(
                  leading: const Icon(Icons.history_outlined),
                  title: const Text('Browsing History'),
                  subtitle: const Text('Recently viewed illustrations and novels'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () {
                    Navigator.of(context).push(
                      MaterialPageRoute(
                        builder: (_) => const HistoryPage(),
                      ),
                    );
                  },
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          Card(
            elevation: 1,
            child: Column(
              children: [
                ListTile(
                  leading: const Icon(Icons.shield_outlined),
                  title: const Text('Anti-Blocking & Network'),
                  subtitle: const Text('Domain Fronting, SNI Delay, Latency Diagnostics'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () {
                    Navigator.of(context).push(
                      MaterialPageRoute(
                        builder: (_) => const NetworkSettingsPage(),
                      ),
                    );
                  },
                ),
                const Divider(height: 1),
                ListTile(
                  leading: const Icon(Icons.storage_outlined),
                  title: const Text('Image Cache'),
                  subtitle: Text(
                    '${cacheState.formattedUsedSize} (${cacheState.entryCount} items in Foyer cache)',
                  ),
                  trailing: TextButton(
                    onPressed: cacheState.isLoading
                        ? null
                        : () async {
                            await ref.read(cacheProvider.notifier).clearCache();
                            if (context.mounted) {
                              ScaffoldMessenger.of(context).showSnackBar(
                                const SnackBar(content: Text('Image cache cleared')),
                              );
                            }
                          },
                    child: const Text('Clear'),
                  ),
                ),
                const Divider(height: 1),
                if (authState.isAuthenticated) ...[
                  ListTile(
                    leading: const Icon(Icons.refresh),
                    title: const Text('Refresh Token Session'),
                    onTap: () async {
                      final messenger = ScaffoldMessenger.of(context);
                      try {
                        await ref.read(authProvider.notifier).refreshSession();
                        messenger.showSnackBar(
                          const SnackBar(content: Text('Session refreshed successfully')),
                        );
                      } catch (e) {
                        messenger.showSnackBar(
                          SnackBar(content: Text('Refresh failed: $e')),
                        );
                      }
                    },
                  ),
                  const Divider(height: 1),
                  ListTile(
                    leading: const Icon(Icons.logout, color: Colors.red),
                    title: const Text('Sign Out', style: TextStyle(color: Colors.red)),
                    onTap: () async {
                      await ref.read(authProvider.notifier).logout();
                    },
                  ),
                ] else ...[
                  ListTile(
                    leading: const Icon(Icons.login),
                    title: const Text('Sign In with Pixiv OAuth'),
                    trailing: const Icon(Icons.chevron_right),
                    onTap: () {
                      Navigator.of(context).push(
                        MaterialPageRoute(
                          builder: (_) => const LoginPage(),
                        ),
                      );
                    },
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class FollowFeedsTab extends ConsumerWidget {
  const FollowFeedsTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authProvider);
    if (!auth.isLoggedIn) {
      return Scaffold(
        appBar: AppBar(title: const Text('Follow Feeds')),
        body: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.lock_outline, size: 64, color: Colors.grey),
              const SizedBox(height: 16),
              const Text('Sign in to view artwork feeds from artists you follow'),
              const SizedBox(height: 16),
              FilledButton.icon(
                onPressed: () {
                  Navigator.of(context).push(
                    MaterialPageRoute(builder: (_) => const LoginPage()),
                  );
                },
                icon: const Icon(Icons.login),
                label: const Text('Sign In'),
              ),
            ],
          ),
        ),
      );
    }

    final feed = ref.watch(followFeedProvider);
    final notifier = ref.read(followFeedProvider.notifier);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Follow Feeds'),
        actions: [
          IconButton(
            icon: const Icon(Icons.search),
            onPressed: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const SearchPage()),
              );
            },
          ),
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () => notifier.refresh(),
          ),
        ],
      ),
      body: ArtworkWaterfall(
        items: feed.items,
        isLoading: feed.isLoading,
        isRefreshing: feed.isRefreshing,
        errorMessage: feed.errorMessage,
        onRefresh: () => notifier.refresh(),
        onLoadMore: () => notifier.loadMore(),
        onBookmarkToggle: (id, bookmarked) => notifier.toggleItemBookmark(id, bookmarked),
      ),
    );
  }
}
