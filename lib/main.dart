import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:path_provider/path_provider.dart';

import 'src/deeplink/deeplink_handler.dart';
import 'src/platform/display_mode_service.dart';
import 'src/rust/api/download.dart';
import 'src/rust/api/events.dart';
import 'src/rust/api/image.dart';
import 'src/rust/api/storage.dart';
import 'src/rust/frb_generated.dart';
import 'src/rust/stream_helpers.dart';
import 'src/state/auth_state.dart';
import 'src/ui/home_shell.dart';
import 'src/ui/login_page.dart';

final GlobalKey<NavigatorState> navigatorKey = GlobalKey<NavigatorState>();
final GlobalKey<ScaffoldMessengerState> scaffoldMessengerKey =
    GlobalKey<ScaffoldMessengerState>();

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Enforce memory bounds on Flutter decoded bitmap cache (128MB / 150 entries)
  PaintingBinding.instance.imageCache.maximumSizeBytes = 128 * 1024 * 1024;
  PaintingBinding.instance.imageCache.maximumSize = 150;

  var rustLibInitialized = false;
  try {
    await RustLib.init();
    rustLibInitialized = true;
  } catch (e, stack) {
    debugPrint('Failed to initialize RustLib: $e\n$stack');
  }

  if (rustLibInitialized) {
    // Parallelize subsystem initialization to accelerate cold-start
    try {
      final dirs = await Future.wait([
        getTemporaryDirectory(),
        getApplicationSupportDirectory(),
      ]);
      final tempDir = dirs[0];
      final supportDir = dirs[1];
      final cachePath = '${tempDir.path}/pixeval_flutter_cache';
      final dbPath = '${supportDir.path}/pixeval_storage.db';

      await Future.wait([
        initImageCache(cacheDir: cachePath).catchError((_) {}),
        initStorage(dbPath: dbPath).catchError((_) {}),
        initDownloadManager(concurrency: 3).catchError((_) {}),
        DisplayModeService.setHighRefreshRate().catchError((_) => 60.0),
      ]);
    } catch (_) {
      // Subsystems fall back gracefully
    }
  }

  runApp(ProviderScope(
    child: PixevalApp(rustLibInitialized: rustLibInitialized),
  ));
}

class PixevalApp extends ConsumerStatefulWidget {
  const PixevalApp({
    super.key,
    this.rustLibInitialized = true,
    this.initialGuestMode = false,
  });

  final bool rustLibInitialized;
  final bool initialGuestMode;

  @override
  ConsumerState<PixevalApp> createState() => _PixevalAppState();
}

class _PixevalAppState extends ConsumerState<PixevalApp>
    with WidgetsBindingObserver {
  StreamSubscription? _sessionSub;
  ProviderSubscription<AuthState>? _authSub;
  final List<String> _pendingLinks = [];
  bool _drainingLinks = false;
  static const MethodChannel _deeplinkChannel = MethodChannel(
    'io.github.pixeval.pixeval_flutter/deeplink',
  );

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    if (widget.rustLibInitialized) {
      _listenSessionEvents();
    }
    if (widget.initialGuestMode) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) ref.read(authProvider.notifier).enterGuestMode();
      });
    }
    _authSub = ref.listenManual(authProvider, (_, _) => _drainLinks());
    _initDeeplinkChannel();
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _sessionSub?.cancel();
    _authSub?.close();
    _deeplinkChannel.setMethodCallHandler(null);
    super.dispose();
  }

  @override
  void didHaveMemoryPressure() {
    super.didHaveMemoryPressure();
    // Flush decoded in-memory image cache on system memory pressure (onTrimMemory)
    PaintingBinding.instance.imageCache.clear();
    PaintingBinding.instance.imageCache.clearLiveImages();
  }

  void _listenSessionEvents() {
    _sessionSub = getAppSessionEventStream().listen((event) {
      event.when(
        authInvalidated: (reason) {
          ref.read(authProvider.notifier).handleAuthInvalidated(reason);
          scaffoldMessengerKey.currentState?.showSnackBar(
            SnackBar(
              content: Text(
                'Pixiv session expired: $reason. Please sign in again.',
              ),
              backgroundColor: Colors.orange[800],
            ),
          );
        },
        rateLimited: (retryAfterSeconds) {
          scaffoldMessengerKey.currentState?.showSnackBar(
            SnackBar(
              content: Text(
                'Pixiv API rate limit hit! Cool down for $retryAfterSeconds seconds.',
              ),
              backgroundColor: Colors.red[800],
            ),
          );
        },
      );
    });
  }

  Future<void> _initDeeplinkChannel() async {
    try {
      _deeplinkChannel.setMethodCallHandler((call) async {
        if (call.method == 'onLink') {
          final link = call.arguments as String?;
          if (link != null && link.isNotEmpty) await _receiveLink(link);
        }
      });

      final links = await _deeplinkChannel.invokeListMethod<String>(
        'getPendingLinks',
      );
      for (final link in links ?? <String>[]) {
        await _receiveLink(link);
      }
    } catch (_) {
      // Deeplink channel may be unavailable on desktop platforms
    }
  }

  Future<void> _receiveLink(String text) async {
    final raw = RegExp(r'(?:https?://|pixiv://|intent://)[^\s<>]+')
        .firstMatch(text)
        ?.group(0);
    final link = (raw ?? text).trim();
    final uri = Uri.tryParse(link);
    if (uri == null || !mounted) return;
    final callback = isPixivCallbackUrl(link);
    if (callback) {
      final success = await ref
          .read(authProvider.notifier)
          .loginWithOAuthInput(link);
      if (success && mounted) {
        navigatorKey.currentState?.popUntil((route) => route.isFirst);
        _drainLinks();
      }
      return;
    }
    if (!_pendingLinks.contains(uri.toString())) {
      _pendingLinks.add(uri.toString());
    }
    WidgetsBinding.instance.addPostFrameCallback((_) => _drainLinks());
    _drainLinks();
  }

  Future<void> _drainLinks() async {
    if (!mounted || _drainingLinks || navigatorKey.currentContext == null) {
      return;
    }
    final auth = ref.read(authProvider);
    if (auth.isLoading || !auth.isLoggedIn) return;
    _drainingLinks = true;
    try {
      while (mounted &&
          _pendingLinks.isNotEmpty &&
          navigatorKey.currentContext != null) {
        final link = _pendingLinks.removeAt(0);
        await DeeplinkHandler.handleUrl(navigatorKey.currentContext!, link);
      }
    } finally {
      _drainingLinks = false;
    }
  }

  @override
  Widget build(BuildContext context) {
    final authState = ref.watch(authProvider);

    Widget homeWidget;
    if (!widget.rustLibInitialized) {
      homeWidget = Scaffold(
        appBar: AppBar(title: const Text('Pixeval Initialization Error')),
        body: Center(
          child: Padding(
            padding: const EdgeInsets.all(24.0),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.error_outline, size: 64, color: Colors.red),
                const SizedBox(height: 16),
                const Text(
                  'Failed to load Rust native library (libpixeval_flutter_bridge.so).',
                  textAlign: TextAlign.center,
                  style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                ),
                const SizedBox(height: 8),
                Text(
                  'Please verify native binaries are compiled for this platform ABI.',
                  textAlign: TextAlign.center,
                  style: TextStyle(color: Theme.of(context).textTheme.bodyMedium?.color),
                ),
              ],
            ),
          ),
        ),
      );
    } else if (authState.isLoading) {
      homeWidget = const Scaffold(
        body: Center(
          child: SizedBox.shrink(),
        ),
      );
    } else if (authState.isLoggedIn || authState.isGuestMode) {
      homeWidget = const HomeShell();
    } else {
      homeWidget = const LoginGuidePage();
    }

    return MaterialApp(
      navigatorKey: navigatorKey,
      scaffoldMessengerKey: scaffoldMessengerKey,
      title: 'Pixeval',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFF0096FA),
          brightness: Brightness.light,
        ),
        useMaterial3: true,
      ),
      darkTheme: ThemeData(
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFF0096FA),
          brightness: Brightness.dark,
        ),
        useMaterial3: true,
      ),
      themeMode: ThemeMode.system,
      home: homeWidget,
    );
  }
}
