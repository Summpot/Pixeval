import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:url_launcher/url_launcher.dart';

import '../rust/api/auth.dart';
import '../state/auth_state.dart';
import 'network_settings_page.dart';

typedef LoginPage = LoginGuidePage;

class LoginGuidePage extends ConsumerStatefulWidget {
  final bool isModal;

  const LoginGuidePage({super.key, this.isModal = false});

  @override
  ConsumerState<LoginGuidePage> createState() => _LoginGuidePageState();
}

class _LoginGuidePageState extends ConsumerState<LoginGuidePage>
    with SingleTickerProviderStateMixin, WidgetsBindingObserver {
  late final TabController _tabController;
  final _tokenController = TextEditingController();
  final _callbackUrlController = TextEditingController();
  final _formKeyToken = GlobalKey<FormState>();
  final _formKeyOAuth = GlobalKey<FormState>();

  PkceAuthParamsDto? _pkceParams;
  String? _savedVerifier;
  String? _lastPastedClipboard;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _tabController = TabController(length: 2, vsync: this);
    _loadExistingVerifier();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) _checkClipboardForCallback();
    });
  }

  Future<void> _loadExistingVerifier() async {
    try {
      final verifier = await ref.read(authProvider.notifier).getSavedVerifier();
      if (verifier != null && verifier.isNotEmpty && mounted) {
        setState(() {
          _savedVerifier = verifier;
        });
      }
    } catch (_) {}
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _tabController.dispose();
    _tokenController.dispose();
    _callbackUrlController.dispose();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      _checkClipboardForCallback();
    }
  }

  Future<void> _checkClipboardForCallback() async {
    if (ref.read(authProvider).isLoading) return;
    try {
      final data = await Clipboard.getData(Clipboard.kTextPlain);
      final text = data?.text?.trim();
      if (text == null || text.isEmpty) return;

      if (isPixivCallbackUrl(text) && text != _lastPastedClipboard && mounted) {
        _lastPastedClipboard = text;
        _callbackUrlController.text = text;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('已自动识别剪贴板中的 Pixiv 授权回调，正在登录...'),
            duration: Duration(seconds: 4),
          ),
        );
        await _handleOAuthLogin();
      }
    } catch (_) {}
  }

  Future<void> _generatePkce() async {
    try {
      final params = await ref
          .read(authProvider.notifier)
          .createOAuthAuthorization();
      if (!mounted) return;
      setState(() {
        _pkceParams = params;
        _savedVerifier = params.codeVerifier;
      });
      final opened = await launchUrl(
        Uri.parse(params.authUrl),
        mode: LaunchMode.externalApplication,
      );
      if (!opened) {
        await Clipboard.setData(ClipboardData(text: params.authUrl));
      }
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              opened
                  ? '已在浏览器打开 Pixiv 授权页，完成授权后复制网址返回。'
                  : '无法自动打开浏览器，授权链接已复制到剪贴板，请手动打开浏览器访问。',
            ),
          ),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('生成授权链接失败: $e')),
        );
      }
    }
  }

  Future<void> _copyAuthUrl() async {
    final url = _pkceParams?.authUrl;
    if (url == null) return;
    await Clipboard.setData(ClipboardData(text: url));
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('授权链接已复制到剪贴板')),
      );
    }
  }

  Future<void> _handleOAuthLogin() async {
    final rawInput = _callbackUrlController.text.trim();
    if (rawInput.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('请输入回调链接或授权码')),
      );
      return;
    }

    final activeVerifier = _pkceParams?.codeVerifier ?? _savedVerifier;
    final success = await ref
        .read(authProvider.notifier)
        .loginWithOAuthInput(rawInput, activeVerifier);

    if (success && mounted) {
      if (widget.isModal && Navigator.of(context).canPop()) {
        Navigator.of(context).pop();
      }
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('登录成功！已自动保存并轮转 Refresh Token。')),
      );
    }
  }

  Future<void> _pasteFromClipboardAndLogin() async {
    try {
      final data = await Clipboard.getData(Clipboard.kTextPlain);
      final text = data?.text?.trim();
      if (text == null || text.isEmpty) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('剪贴板中没有内容')),
          );
        }
        return;
      }
      _callbackUrlController.text = text;
      await _handleOAuthLogin();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('读取剪贴板失败: $e')),
        );
      }
    }
  }

  Future<void> _handleTokenLogin() async {
    if (!_formKeyToken.currentState!.validate()) return;
    final token = _tokenController.text.trim();
    final success = await ref
        .read(authProvider.notifier)
        .loginWithRefreshToken(token);
    if (success && mounted) {
      if (widget.isModal && Navigator.of(context).canPop()) {
        Navigator.of(context).pop();
      }
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('登录成功！')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final authState = ref.watch(authProvider);
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Pixiv 登录指引'),
        automaticallyImplyLeading: widget.isModal,
        actions: [
          IconButton(
            icon: const Icon(Icons.network_check_rounded),
            tooltip: '网络与免代理设置',
            onPressed: () {
              Navigator.of(context).push(
                MaterialPageRoute(
                  builder: (_) => const NetworkSettingsPage(),
                ),
              );
            },
          ),
        ],
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.symmetric(horizontal: 20.0, vertical: 12.0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              // Hero Branding Section
              _buildHeroSection(theme, colorScheme),
              const SizedBox(height: 16),

              // Why Login is Required Explanation
              _buildWhyLoginCard(theme, colorScheme),
              const SizedBox(height: 12),

              // Network Advisory Card
              _buildNetworkAdvisoryCard(theme, colorScheme),
              const SizedBox(height: 16),

              // TabBar for Login Methods
              Container(
                decoration: BoxDecoration(
                  color: colorScheme.surfaceContainerHighest.withValues(alpha: 0.5),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: TabBar(
                  controller: _tabController,
                  indicatorSize: TabBarIndicatorSize.tab,
                  dividerColor: Colors.transparent,
                  tabs: const [
                    Tab(
                      icon: Icon(Icons.open_in_browser_rounded),
                      text: 'OAuth 网页授权登录',
                    ),
                    Tab(
                      icon: Icon(Icons.vpn_key_rounded),
                      text: 'Refresh Token 登录',
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),

              // TabBar Content (using AnimatedBuilder/index to keep scrollable inside SingleChildScrollView)
              AnimatedBuilder(
                animation: _tabController,
                builder: (context, _) {
                  return _tabController.index == 0
                      ? _buildOAuthSection(theme, colorScheme, authState)
                      : _buildTokenSection(theme, colorScheme, authState);
                },
              ),
              const SizedBox(height: 24),

              // Collapsible FAQ Section
              _buildFaqSection(theme, colorScheme),
              const SizedBox(height: 24),

              // Guest Mode / Skip Login Button
              if (!widget.isModal) ...[
                Center(
                  child: Column(
                    children: [
                      TextButton.icon(
                        key: const Key('guest_mode_button'),
                        icon: const Icon(Icons.arrow_forward_rounded),
                        label: const Text(
                          '暂不登录，以访客模式浏览',
                          style: TextStyle(fontSize: 15, fontWeight: FontWeight.w600),
                        ),
                        onPressed: () {
                          ref.read(authProvider.notifier).enterGuestMode();
                        },
                      ),
                      const SizedBox(height: 4),
                      Text(
                        '访客模式下仅可查看本地历史与设置，无法获取 Pixiv 在线作品',
                        textAlign: TextAlign.center,
                        style: TextStyle(
                          fontSize: 12,
                          color: colorScheme.onSurfaceVariant,
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 24),
              ],
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildHeroSection(ThemeData theme, ColorScheme colorScheme) {
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 20, horizontal: 16),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: [
            const Color(0xFF0096FA).withValues(alpha: 0.15),
            colorScheme.primary.withValues(alpha: 0.05),
          ],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: const Color(0xFF0096FA).withValues(alpha: 0.2),
        ),
      ),
      child: Column(
        children: [
          Container(
            width: 68,
            height: 68,
            decoration: BoxDecoration(
              gradient: const LinearGradient(
                colors: [Color(0xFF0096FA), Color(0xFF0070BA)],
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
              ),
              borderRadius: BorderRadius.circular(18),
              boxShadow: [
                BoxShadow(
                  color: const Color(0xFF0096FA).withValues(alpha: 0.35),
                  blurRadius: 16,
                  offset: const Offset(0, 6),
                ),
              ],
            ),
            child: const Icon(
              Icons.palette_rounded,
              size: 38,
              color: Colors.white,
            ),
          ),
          const SizedBox(height: 14),
          Text(
            '欢迎使用 Pixeval',
            style: theme.textTheme.headlineSmall?.copyWith(
              fontWeight: FontWeight.bold,
              letterSpacing: -0.5,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            'Pixiv 开源插画与小说客户端',
            style: theme.textTheme.bodyMedium?.copyWith(
              color: colorScheme.onSurfaceVariant,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildWhyLoginCard(ThemeData theme, ColorScheme colorScheme) {
    return Card(
      elevation: 0,
      color: colorScheme.primaryContainer.withValues(alpha: 0.35),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(14),
        side: BorderSide(
          color: colorScheme.primary.withValues(alpha: 0.25),
        ),
      ),
      child: Padding(
        padding: const EdgeInsets.all(14.0),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(
              Icons.info_outline_rounded,
              color: colorScheme.primary,
              size: 22,
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    '为什么必须登录 Pixiv？',
                    style: theme.textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.bold,
                      color: colorScheme.primary,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    '由于 Pixiv 官方 API 鉴权政策，Pixiv 客户端在未登录时无法加载推荐、排行榜、原图及搜索结果。登录后方可畅快探索海量作品，并无缝同步您的关注画师、书签收藏与浏览记录。',
                    style: theme.textTheme.bodySmall?.copyWith(
                      height: 1.45,
                      color: colorScheme.onSurfaceVariant,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildNetworkAdvisoryCard(ThemeData theme, ColorScheme colorScheme) {
    return Card(
      elevation: 0,
      color: Colors.amber.withValues(alpha: 0.12),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(14),
        side: BorderSide(
          color: Colors.amber.withValues(alpha: 0.35),
        ),
      ),
      child: Padding(
        padding: const EdgeInsets.all(14.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                const Icon(
                  Icons.shield_outlined,
                  color: Colors.amber,
                  size: 22,
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Text(
                    '无法打开登录页面或网络受阻？',
                    style: theme.textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.bold,
                      color: Colors.amber[900],
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 6),
            Text(
              '在部分地区（如中国大陆），Pixiv 受到域名污染或 SNI 连接阻断。Pixeval 内置了 SNI 分片与 IP 直连技术。若遇到连接超时，可前往配置。',
              style: theme.textTheme.bodySmall?.copyWith(
                height: 1.4,
                color: Colors.brown[700],
              ),
            ),
            const SizedBox(height: 10),
            Align(
              alignment: Alignment.centerLeft,
              child: OutlinedButton.icon(
                style: OutlinedButton.styleFrom(
                  foregroundColor: Colors.amber[900],
                  side: BorderSide(color: Colors.amber.withValues(alpha: 0.6)),
                  visualDensity: VisualDensity.compact,
                ),
                onPressed: () {
                  Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (_) => const NetworkSettingsPage(),
                    ),
                  );
                },
                icon: const Icon(Icons.tune_rounded, size: 16),
                label: const Text('网络与免代理设置'),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildOAuthSection(
    ThemeData theme,
    ColorScheme colorScheme,
    AuthState authState,
  ) {
    final activeVerifier = _pkceParams?.codeVerifier ?? _savedVerifier;
    final isVerifierReady = activeVerifier != null && activeVerifier.isNotEmpty;
    final inputTrim = _callbackUrlController.text.trim();
    final isLoginUrl = inputTrim.contains('/web/v1/login') || inputTrim.contains('code_challenge=');

    return Form(
      key: _formKeyOAuth,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // Step 1 Card
          _buildStepCard(
            stepNumber: '1',
            title: '发起官方网页授权',
            description: '点击下方按钮生成专属安全授权链接，并在系统浏览器中登录您的 Pixiv 账号。',
            theme: theme,
            colorScheme: colorScheme,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                FilledButton.icon(
                  onPressed: authState.isLoading ? null : _generatePkce,
                  icon: const Icon(Icons.open_in_browser_rounded),
                  label: Text(
                    isVerifierReady ? '重新生成授权链接' : '生成授权并在浏览器打开',
                  ),
                ),
                if (isVerifierReady) ...[
                  const SizedBox(height: 8),
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 10,
                      vertical: 6,
                    ),
                    decoration: BoxDecoration(
                      color: Colors.green.withValues(alpha: 0.1),
                      borderRadius: BorderRadius.circular(8),
                      border: Border.all(
                        color: Colors.green.withValues(alpha: 0.3),
                      ),
                    ),
                    child: Row(
                      children: [
                        const Icon(
                          Icons.check_circle_rounded,
                          size: 16,
                          color: Colors.green,
                        ),
                        const SizedBox(width: 6),
                        Expanded(
                          child: Text(
                            '授权链接就绪 (Verifier: ${activeVerifier.length >= 8 ? activeVerifier.substring(0, 8) : activeVerifier}...)',
                            style: const TextStyle(
                              fontSize: 12,
                              color: Colors.green,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ),
                        if (_pkceParams?.authUrl != null)
                          TextButton(
                            onPressed: _copyAuthUrl,
                            style: TextButton.styleFrom(
                              padding: const EdgeInsets.symmetric(horizontal: 8),
                              minimumSize: Size.zero,
                              tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                            ),
                            child: const Text('复制链接', style: TextStyle(fontSize: 12)),
                          ),
                      ],
                    ),
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(height: 12),

          // Step 2 Card
          _buildStepCard(
            stepNumber: '2',
            title: '浏览器登录与回调提示',
            description:
                '在打开的浏览器中完成 Pixiv 账号登录并确认授权。\n\n💡 重要排错提示：\n授权完成后浏览器会尝试跳转回到应用。若浏览器提示【无法打开网页】、net::ERR_UNKNOWN_URL_SCHEME 或显示空白页，这是正常现象！请直接复制浏览器顶部地址栏中的完整网址。',
            theme: theme,
            colorScheme: colorScheme,
          ),
          const SizedBox(height: 12),

          // Step 3 Card
          _buildStepCard(
            stepNumber: '3',
            title: '获取授权码并完成登录',
            description: '返回本应用，您可以直接从剪贴板读取或在下方粘贴回调链接/Code：',
            theme: theme,
            colorScheme: colorScheme,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                OutlinedButton.icon(
                  onPressed: authState.isLoading ? null : _pasteFromClipboardAndLogin,
                  icon: const Icon(Icons.content_paste_go_rounded),
                  label: const Text('从剪贴板读取并一键登录'),
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: _callbackUrlController,
                  decoration: InputDecoration(
                    labelText: '回调 URL 或 Code',
                    hintText: '粘贴包含 code=... 的完整链接或授权码',
                    prefixIcon: const Icon(Icons.link_rounded),
                    suffixIcon: _callbackUrlController.text.isNotEmpty
                        ? IconButton(
                            icon: const Icon(Icons.clear),
                            onPressed: () {
                              setState(() {
                                _callbackUrlController.clear();
                              });
                            },
                          )
                        : IconButton(
                            icon: const Icon(Icons.paste_rounded),
                            tooltip: '粘贴',
                            onPressed: () async {
                              final data = await Clipboard.getData(Clipboard.kTextPlain);
                              if (data?.text != null) {
                                setState(() {
                                  _callbackUrlController.text = data!.text!.trim();
                                });
                              }
                            },
                          ),
                    border: const OutlineInputBorder(),
                  ),
                  onChanged: (_) => setState(() {}),
                ),
                if (isLoginUrl) ...[
                  const SizedBox(height: 8),
                  Container(
                    padding: const EdgeInsets.all(10),
                    decoration: BoxDecoration(
                      color: Colors.amber.withValues(alpha: 0.1),
                      borderRadius: BorderRadius.circular(8),
                      border: Border.all(color: Colors.amber.withValues(alpha: 0.4)),
                    ),
                    child: const Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Icon(Icons.warning_amber_rounded, color: Colors.orange, size: 18),
                        SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            '提示：当前输入框中是发起授权的登录链接（包含 code_challenge），而非授权完成后的回调链接。\n请在打开的浏览器中完成登录，若网页提示【无法打开网页】，复制浏览器地址栏中包含 code= 的完整网址即可。',
                            style: TextStyle(color: Colors.orange, fontSize: 12, height: 1.3),
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
                if (authState.errorMessage != null) ...[
                  const SizedBox(height: 8),
                  Container(
                    padding: const EdgeInsets.all(10),
                    decoration: BoxDecoration(
                      color: Colors.red.withValues(alpha: 0.08),
                      borderRadius: BorderRadius.circular(8),
                      border: Border.all(color: Colors.red.withValues(alpha: 0.3)),
                    ),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Icon(Icons.error_outline_rounded, color: Colors.red, size: 18),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            authState.errorMessage!,
                            style: const TextStyle(color: Colors.red, fontSize: 13, height: 1.35),
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
                const SizedBox(height: 14),
                FilledButton.icon(
                  onPressed: authState.isLoading ? null : _handleOAuthLogin,
                  icon: authState.isLoading
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: Colors.white,
                        ),
                      )
                    : const Icon(Icons.login_rounded),
                  label: Text(
                    authState.isLoading ? '正在验证并换取 Token...' : '完成授权并登录',
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildTokenSection(
    ThemeData theme,
    ColorScheme colorScheme,
    AuthState authState,
  ) {
    return Form(
      key: _formKeyToken,
      child: Card(
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(14),
          side: BorderSide(
            color: colorScheme.outlineVariant.withValues(alpha: 0.5),
          ),
        ),
        child: Padding(
          padding: const EdgeInsets.all(16.0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  Container(
                    padding: const EdgeInsets.all(8),
                    decoration: BoxDecoration(
                      color: colorScheme.primaryContainer.withValues(alpha: 0.5),
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: Icon(
                      Icons.vpn_key_outlined,
                      color: colorScheme.primary,
                      size: 20,
                    ),
                  ),
                  const SizedBox(width: 12),
                  Text(
                    '使用已有 Refresh Token 登录',
                    style: theme.textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 10),
              Text(
                '如果您此前已在 Pixeval 桌面端、Shaft、PixEz 或网页中获取过 Pixiv Refresh Token，可直接粘贴登录。登录成功后凭证将自动加密持久化并自动维护 Token 轮转。',
                style: theme.textTheme.bodySmall?.copyWith(
                  height: 1.45,
                  color: colorScheme.onSurfaceVariant,
                ),
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _tokenController,
                decoration: InputDecoration(
                  labelText: 'OAuth Refresh Token',
                  hintText: '在此粘贴 refresh_token',
                  prefixIcon: const Icon(Icons.key_rounded),
                  suffixIcon: _tokenController.text.isNotEmpty
                      ? IconButton(
                          icon: const Icon(Icons.clear),
                          onPressed: () {
                            setState(() {
                              _tokenController.clear();
                            });
                          },
                        )
                      : IconButton(
                          icon: const Icon(Icons.paste_rounded),
                          tooltip: '粘贴',
                          onPressed: () async {
                            final data = await Clipboard.getData(Clipboard.kTextPlain);
                            if (data?.text != null) {
                              setState(() {
                                _tokenController.text = data!.text!.trim();
                              });
                            }
                          },
                        ),
                  border: const OutlineInputBorder(),
                ),
                validator: (val) {
                  if (val == null || val.trim().isEmpty) {
                    return '请输入有效的 Refresh Token';
                  }
                  return null;
                },
                onChanged: (_) => setState(() {}),
              ),
              if (authState.errorMessage != null) ...[
                const SizedBox(height: 8),
                Text(
                  authState.errorMessage!,
                  style: const TextStyle(color: Colors.red, fontSize: 13),
                ),
              ],
              const SizedBox(height: 16),
              FilledButton.icon(
                onPressed: authState.isLoading ? null : _handleTokenLogin,
                icon: authState.isLoading
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: Colors.white,
                        ),
                      )
                    : const Icon(Icons.login_rounded),
                label: Text(authState.isLoading ? '正在登录...' : '使用 Token 登录'),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildStepCard({
    required String stepNumber,
    required String title,
    required String description,
    required ThemeData theme,
    required ColorScheme colorScheme,
    Widget? child,
  }) {
    return Card(
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(14),
        side: BorderSide(
          color: colorScheme.outlineVariant.withValues(alpha: 0.5),
        ),
      ),
      child: Padding(
        padding: const EdgeInsets.all(14.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                CircleAvatar(
                  radius: 12,
                  backgroundColor: colorScheme.primary,
                  child: Text(
                    stepNumber,
                    style: TextStyle(
                      color: colorScheme.onPrimary,
                      fontSize: 12,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
                const SizedBox(width: 10),
                Text(
                  title,
                  style: theme.textTheme.titleSmall?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Text(
              description,
              style: theme.textTheme.bodySmall?.copyWith(
                height: 1.45,
                color: colorScheme.onSurfaceVariant,
              ),
            ),
            if (child != null) ...[
              const SizedBox(height: 12),
              child,
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildFaqSection(ThemeData theme, ColorScheme colorScheme) {
    return Card(
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(14),
        side: BorderSide(
          color: colorScheme.outlineVariant.withValues(alpha: 0.5),
        ),
      ),
      child: ExpansionTile(
        title: Text(
          '登录遇到问题？查看排错指南',
          style: theme.textTheme.titleSmall?.copyWith(
            fontWeight: FontWeight.bold,
          ),
        ),
        leading: Icon(Icons.help_outline_rounded, color: colorScheme.primary),
        childrenPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        children: const [
          _FaqItem(
            question: '1. 点击生成后浏览器打不开或连接超时？',
            answer:
                'Pixiv 域名在国内受到 SNI 干扰与网络阻断。请点击页面顶部的【网络与免代理设置】，开启内置 SNI 分片延迟（推荐 100ms），或在系统设置中启用科学上网工具。',
          ),
          Divider(height: 16),
          _FaqItem(
            question: '2. 授权后浏览器报错“无法打开网页”或页面空白？',
            answer:
                '这是完全正常的现象。Pixiv 在授权完成后会使用 pixiv:// 协议重定向唤起客户端，普通浏览器无法识别该协议因此会报错。您只需长按复制当前浏览器地址栏里的完整网址，返回 Pixeval 点击【从剪贴板读取并一键登录】即可。',
          ),
          Divider(height: 16),
          _FaqItem(
            question: '3. 账号安全与隐私说明',
            answer:
                'Pixeval 是完全开源的客户端，严格采用官方 OAuth 2.0 PKCE 开放授权规范。所有授权在 Pixiv 官方网页完成，Pixeval 不会接触也不保存您的账号密码，凭据与 Token 仅安全保存在您的本地设备中。',
          ),
          Divider(height: 16),
          _FaqItem(
            question: '4. 不登录真的不能使用吗？',
            answer:
                '是的。由于 Pixiv 官方对 API 全面强制校验 Bearer Token 凭据，未登录时 Pixiv 官方服务器会直接拒绝响应插画详情、原图及搜索接口。',
          ),
          SizedBox(height: 8),
        ],
      ),
    );
  }
}

class _FaqItem extends StatelessWidget {
  final String question;
  final String answer;

  const _FaqItem({required this.question, required this.answer});

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          question,
          style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 13),
        ),
        const SizedBox(height: 4),
        Text(
          answer,
          style: TextStyle(
            color: Theme.of(context).colorScheme.onSurfaceVariant,
            fontSize: 12,
            height: 1.45,
          ),
        ),
      ],
    );
  }
}
