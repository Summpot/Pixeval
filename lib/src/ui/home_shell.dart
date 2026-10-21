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
    final authState = ref.watch(authProvider);

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
          if (authState.isLoggedIn)
            IconButton(
              icon: const Icon(Icons.refresh),
              onPressed: () => ref.read(recommendedFeedProvider.notifier).refresh(),
            ),
        ],
      ),
      body: !authState.isLoggedIn
          ? const _UnauthenticatedTabPrompt(
              icon: Icons.lock_person_outlined,
              title: '需要登录 Pixiv 账号',
              description:
                  'Pixiv 官方现已全面限制未登录访问。登录后即可畅快浏览每日精选推荐、相关画作与海量作品。',
            )
          : Consumer(
              builder: (context, ref, _) {
                final feed = ref.watch(recommendedFeedProvider);
                final notifier = ref.read(recommendedFeedProvider.notifier);
                return ArtworkWaterfall(
                  items: feed.items,
                  isLoading: feed.isLoading,
                  isRefreshing: feed.isRefreshing,
                  errorMessage: feed.errorMessage,
                  onRefresh: () => notifier.refresh(),
                  onLoadMore: () => notifier.loadMore(),
                  onBookmarkToggle: (id, bookmarked) =>
                      notifier.toggleItemBookmark(id, bookmarked),
                );
              },
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
    final authState = ref.watch(authProvider);

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
          if (authState.isLoggedIn)
            IconButton(
              icon: const Icon(Icons.refresh),
              onPressed: () => ref.read(rankingFeedProvider.notifier).refresh(),
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
                final isSelected = ref.watch(rankingFeedProvider.notifier).mode == mode.$1;
                return ChoiceChip(
                  label: Text(mode.$2),
                  selected: isSelected,
                  onSelected: authState.isLoggedIn
                      ? (selected) {
                          if (selected) {
                            ref.read(rankingFeedProvider.notifier).setMode(mode.$1);
                          }
                        }
                      : null,
                );
              },
            ),
          ),
        ),
      ),
      body: !authState.isLoggedIn
          ? const _UnauthenticatedTabPrompt(
              icon: Icons.leaderboard_outlined,
              title: '登录以查看 Pixiv 排行榜',
              description:
                  'Pixiv 排行榜接口需要账号身份鉴权。登录后即可畅快浏览日榜、周榜、月榜、新人榜及男女向榜单。',
            )
          : Consumer(
              builder: (context, ref, _) {
                final feed = ref.watch(rankingFeedProvider);
                final notifier = ref.read(rankingFeedProvider.notifier);
                return ArtworkWaterfall(
                  items: feed.items,
                  isLoading: feed.isLoading,
                  isRefreshing: feed.isRefreshing,
                  errorMessage: feed.errorMessage,
                  onRefresh: () => notifier.refresh(),
                  onLoadMore: () => notifier.loadMore(),
                  onBookmarkToggle: (id, bookmarked) =>
                      notifier.toggleItemBookmark(id, bookmarked),
                );
              },
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
                          user != null
                              ? '@${user.account}'
                              : '登录 Pixiv 账号以同步画师关注与作品收藏',
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
                    title: const Text('Pixiv 账号登录'),
                    trailing: const Icon(Icons.chevron_right),
                    onTap: () {
                      Navigator.of(context).push(
                        MaterialPageRoute(
                          builder: (_) => const LoginGuidePage(isModal: true),
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
        body: const _UnauthenticatedTabPrompt(
          icon: Icons.dynamic_feed_rounded,
          title: '登录查看已关注画师动态',
          description: '登录您的 Pixiv 账号即可自动同步关注列表，实时追更关注画师的最新作品。',
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

class _UnauthenticatedTabPrompt extends StatelessWidget {
  final IconData icon;
  final String title;
  final String description;

  const _UnauthenticatedTabPrompt({
    required this.icon,
    required this.title,
    required this.description,
  });

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.symmetric(horizontal: 24.0, vertical: 32.0),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              padding: const EdgeInsets.all(20),
              decoration: BoxDecoration(
                color: colorScheme.primaryContainer.withValues(alpha: 0.4),
                shape: BoxShape.circle,
              ),
              child: Icon(icon, size: 54, color: colorScheme.primary),
            ),
            const SizedBox(height: 20),
            Text(
              title,
              textAlign: TextAlign.center,
              style: theme.textTheme.titleLarge?.copyWith(
                fontWeight: FontWeight.bold,
              ),
            ),
            const SizedBox(height: 8),
            ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 380),
              child: Text(
                description,
                textAlign: TextAlign.center,
                style: TextStyle(
                  color: colorScheme.onSurfaceVariant,
                  fontSize: 14,
                  height: 1.5,
                ),
              ),
            ),
            const SizedBox(height: 24),
            FilledButton.icon(
              onPressed: () {
                Navigator.of(context).push(
                  MaterialPageRoute(
                    builder: (_) => const LoginGuidePage(isModal: true),
                  ),
                );
              },
              icon: const Icon(Icons.login_rounded),
              label: const Text('前往登录'),
            ),
            const SizedBox(height: 10),
            OutlinedButton.icon(
              onPressed: () {
                Navigator.of(context).push(
                  MaterialPageRoute(
                    builder: (_) => const NetworkSettingsPage(),
                  ),
                );
              },
              icon: const Icon(Icons.network_check_rounded),
              label: const Text('网络与免代理设置'),
            ),
          ],
        ),
      ),
    );
  }
}
