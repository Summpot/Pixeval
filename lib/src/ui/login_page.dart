import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:url_launcher/url_launcher.dart';

import '../constants/app_assets.dart';
import '../native/api/auth.dart';
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
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text('生成授权链接失败: $e')));
      }
    }
  }

  Future<void> _copyAuthUrl() async {
    final url = _pkceParams?.authUrl;
    if (url == null) return;
    await Clipboard.setData(ClipboardData(text: url));
    if (mounted) {
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('授权链接已复制到剪贴板')));
    }
  }

  Future<void> _handleOAuthLogin() async {
    final rawInput = _callbackUrlController.text.trim();
    if (rawInput.isEmpty) {
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('请输入回调链接或授权码')));
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
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('登录成功！已自动保存凭据。')));
    }
  }

  Future<void> _pasteFromClipboardAndLogin() async {
    try {
      final data = await Clipboard.getData(Clipboard.kTextPlain);
      final text = data?.text?.trim();
      if (text == null || text.isEmpty) {
        if (mounted) {
          ScaffoldMessenger.of(context)
              .showSnackBar(const SnackBar(content: Text('剪贴板中没有内容')));
        }
        return;
      }
      _callbackUrlController.text = text;
      await _handleOAuthLogin();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text('读取剪贴板失败: $e')));
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
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('登录成功！')));
    }
  }

  void _showHelpSheet(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (ctx) => DraggableScrollableSheet(
        initialChildSize: 0.65,
        maxChildSize: 0.9,
        minChildSize: 0.4,
        expand: false,
        builder: (_, scrollController) => ListView(
          controller: scrollController,
          padding: const EdgeInsets.fromLTRB(20, 0, 20, 24),
          children: [
            Row(
              children: [
                Icon(Icons.help_outline_rounded, color: colorScheme.primary),
                const SizedBox(width: 8),
                Text(
                  '登录排错与帮助指南',
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
            Card(
              elevation: 0,
              color: Colors.amber.withValues(alpha: 0.12),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(12),
                side: BorderSide(color: Colors.amber.withValues(alpha: 0.35)),
              ),
              child: Padding(
                padding: const EdgeInsets.all(14),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        const Icon(
                          Icons.shield_outlined,
                          color: Colors.amber,
                          size: 20,
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            '网络受阻或打不开网页？',
                            style: TextStyle(
                              fontWeight: FontWeight.bold,
                              color: Colors.amber[900],
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 6),
                    Text(
                      'Pixiv 在部分地区受到 SNI 连接干扰。Pixeval 内置了 SNI 分片与免代理直连能力，可前往进行测速与配置。',
                      style: TextStyle(
                        fontSize: 13,
                        color: Colors.brown[700],
                        height: 1.4,
                      ),
                    ),
                    const SizedBox(height: 10),
                    FilledButton.tonalIcon(
                      onPressed: () {
                        Navigator.of(ctx).pop();
                        Navigator.of(context).push(
                          MaterialPageRoute(
                            builder: (_) => const NetworkSettingsPage(),
                          ),
                        );
                      },
                      icon: const Icon(Icons.tune_rounded, size: 16),
                      label: const Text('前往网络与免代理设置'),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 16),
            Text(
              '常见问题',
              style: theme.textTheme.titleSmall?.copyWith(
                fontWeight: FontWeight.bold,
              ),
            ),
            const SizedBox(height: 8),
            _buildFaqItem(
              theme,
              colorScheme,
              question: '1. 授权后浏览器提示“无法打开网页”或显示空白？',
              answer: '这是正常现象！Pixiv 授权完成后会尝试唤起 pixiv:// 协议，浏览器无法直接识别。只需复制当前浏览器地址栏中的完整网址，返回 Pixeval 粘贴即可登录。',
            ),
            _buildFaqItem(
              theme,
              colorScheme,
              question: '2. 为什么需要登录 Pixiv？',
              answer: 'Pixiv 官方 API 接口强制校验登录凭证，未登录状态下无法获取插画详情、原图以及搜索结果。',
            ),
            _buildFaqItem(
              theme,
              colorScheme,
              question: '3. 账号与数据安全说明',
              answer: 'Pixeval 遵循官方 OAuth 2.0 PKCE 规范，所有登录都在 Pixiv 官方网页完成，应用不会接触您的账号密码，凭据仅保存在本地设备。',
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildFaqItem(
    ThemeData theme,
    ColorScheme colorScheme, {
    required String question,
    required String answer,
  }) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: colorScheme.surfaceContainerHighest.withValues(alpha: 0.35),
          borderRadius: BorderRadius.circular(10),
        ),
        child: Column(
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
                color: colorScheme.onSurfaceVariant,
                fontSize: 12,
                height: 1.4,
              ),
            ),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final authState = ref.watch(authProvider);
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Scaffold(
      appBar: AppBar(
        title: Text(widget.isModal ? '登录 Pixiv' : ''),
        automaticallyImplyLeading: widget.isModal,
        actions: [
          IconButton(
            icon: const Icon(Icons.network_check_rounded),
            tooltip: '网络与免代理设置',
            onPressed: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const NetworkSettingsPage()),
              );
            },
          ),
          IconButton(
            icon: const Icon(Icons.help_outline_rounded),
            tooltip: '登录帮助',
            onPressed: () => _showHelpSheet(context),
          ),
        ],
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 440),
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(
                horizontal: 24.0,
                vertical: 12.0,
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  _buildHeader(theme, colorScheme),
                  const SizedBox(height: 24),
                  Container(
                    decoration: BoxDecoration(
                      color: colorScheme.surfaceContainerHighest.withValues(
                        alpha: 0.5,
                      ),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: TabBar(
                      controller: _tabController,
                      indicatorSize: TabBarIndicatorSize.tab,
                      dividerColor: Colors.transparent,
                      tabs: const [
                        Tab(
                          icon: Icon(Icons.open_in_browser_rounded, size: 20),
                          text: 'OAuth 网页授权登录',
                        ),
                        Tab(
                          icon: Icon(Icons.vpn_key_rounded, size: 20),
                          text: 'Refresh Token 登录',
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 20),
                  AnimatedBuilder(
                    animation: _tabController,
                    builder: (context, _) {
                      return _tabController.index == 0
                          ? _buildOAuthTab(theme, colorScheme, authState)
                          : _buildTokenTab(theme, colorScheme, authState);
                    },
                  ),
                  const SizedBox(height: 20),
                  if (!widget.isModal) ...[
                    Center(
                      child: TextButton.icon(
                        key: const Key('guest_mode_button'),
                        icon: const Icon(Icons.arrow_forward_rounded, size: 18),
                        label: const Text(
                          '暂不登录，以访客模式浏览',
                          style: TextStyle(
                            fontSize: 14,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                        onPressed: () {
                          ref.read(authProvider.notifier).enterGuestMode();
                        },
                      ),
                    ),
                  ],
                  Center(
                    child: TextButton.icon(
                      onPressed: () => _showHelpSheet(context),
                      icon: const Icon(Icons.help_outline_rounded, size: 15),
                      label: const Text(
                        '遇到登录问题？查看排错指南',
                        style: TextStyle(fontSize: 12),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildHeader(ThemeData theme, ColorScheme colorScheme) {
    return Column(
      children: [
        Container(
          width: 64,
          height: 64,
          decoration: BoxDecoration(
            color: const Color(0xFF0096FA),
            borderRadius: BorderRadius.circular(16),
            boxShadow: [
              BoxShadow(
                color: const Color(0xFF0096FA).withValues(alpha: 0.3),
                blurRadius: 16,
                offset: const Offset(0, 4),
              ),
            ],
          ),
          child: ClipRRect(
            borderRadius: BorderRadius.circular(16),
            child: Image.asset(
              AppAssets.logo,
              fit: BoxFit.cover,
              errorBuilder: (_, _, _) => const Icon(
                Icons.palette_rounded,
                size: 36,
                color: Colors.white,
              ),
            ),
          ),
        ),
        const SizedBox(height: 16),
        Text(
          '欢迎使用 Pixeval',
          style: theme.textTheme.headlineSmall?.copyWith(
            fontWeight: FontWeight.bold,
            letterSpacing: -0.5,
          ),
        ),
        const SizedBox(height: 6),
        Text(
          '登录您的 Pixiv 账号以同步收藏与关注',
          style: theme.textTheme.bodyMedium?.copyWith(
            color: colorScheme.onSurfaceVariant,
          ),
        ),
      ],
    );
  }

  Widget _buildOAuthTab(
    ThemeData theme,
    ColorScheme colorScheme,
    AuthState authState,
  ) {
    final activeVerifier = _pkceParams?.codeVerifier ?? _savedVerifier;
    final isVerifierReady = activeVerifier != null && activeVerifier.isNotEmpty;
    final inputTrim = _callbackUrlController.text.trim();
    final isLoginUrl =
        inputTrim.contains('/web/v1/login') ||
        inputTrim.contains('code_challenge=');

    return Form(
      key: _formKeyOAuth,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          FilledButton.icon(
            style: FilledButton.styleFrom(
              padding: const EdgeInsets.symmetric(vertical: 14),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(12),
              ),
            ),
            onPressed: authState.isLoading ? null : _generatePkce,
            icon: const Icon(Icons.open_in_browser_rounded),
            label: Text(
              isVerifierReady ? '重新在浏览器打开授权页' : '打开 Pixiv 网页登录',
              style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600),
            ),
          ),
          if (isVerifierReady && _pkceParams?.authUrl != null) ...[
            const SizedBox(height: 4),
            Align(
              alignment: Alignment.center,
              child: TextButton.icon(
                onPressed: _copyAuthUrl,
                icon: const Icon(Icons.copy_rounded, size: 14),
                label: const Text('复制授权链接到剪贴板', style: TextStyle(fontSize: 12)),
              ),
            ),
          ] else ...[
            const SizedBox(height: 12),
          ],
          Row(
            children: [
              Expanded(
                child: Divider(
                  color: colorScheme.outlineVariant.withValues(alpha: 0.5),
                ),
              ),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 10),
                child: Text(
                  '完成授权后返回',
                  style: TextStyle(
                    fontSize: 12,
                    color: colorScheme.onSurfaceVariant,
                  ),
                ),
              ),
              Expanded(
                child: Divider(
                  color: colorScheme.outlineVariant.withValues(alpha: 0.5),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          TextFormField(
            controller: _callbackUrlController,
            decoration: InputDecoration(
              labelText: '回调链接或授权码',
              hintText: '粘贴包含 code= 的网址或授权码',
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
                        final data = await Clipboard.getData(
                          Clipboard.kTextPlain,
                        );
                        if (data?.text != null) {
                          setState(() {
                            _callbackUrlController.text = data!.text!.trim();
                          });
                        }
                      },
                    ),
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(12),
              ),
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
                  Icon(
                    Icons.warning_amber_rounded,
                    color: Colors.orange,
                    size: 18,
                  ),
                  SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      '提示：当前输入框中是发起授权的登录链接（包含 code_challenge），而非授权完成后的回调链接。\n请在打开的浏览器中完成登录，若网页提示【无法打开网页】，复制浏览器地址栏中包含 code= 的完整网址即可。',
                      style: TextStyle(
                        color: Colors.orange,
                        fontSize: 12,
                        height: 1.3,
                      ),
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
                  const Icon(
                    Icons.error_outline_rounded,
                    color: Colors.red,
                    size: 18,
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      authState.errorMessage!,
                      style: const TextStyle(
                        color: Colors.red,
                        fontSize: 13,
                        height: 1.35,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
          const SizedBox(height: 14),
          Row(
            children: [
              Expanded(
                child: OutlinedButton.icon(
                  style: OutlinedButton.styleFrom(
                    padding: const EdgeInsets.symmetric(vertical: 12),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(10),
                    ),
                  ),
                  onPressed: authState.isLoading
                      ? null
                      : _pasteFromClipboardAndLogin,
                  icon: const Icon(Icons.content_paste_go_rounded, size: 18),
                  label: const Text('从剪贴板粘贴'),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: FilledButton.icon(
                  style: FilledButton.styleFrom(
                    padding: const EdgeInsets.symmetric(vertical: 12),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(10),
                    ),
                  ),
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
                      : const Icon(Icons.login_rounded, size: 18),
                  label: Text(authState.isLoading ? '验证中...' : '完成登录'),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildTokenTab(
    ThemeData theme,
    ColorScheme colorScheme,
    AuthState authState,
  ) {
    return Form(
      key: _formKeyToken,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
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
                        final data = await Clipboard.getData(
                          Clipboard.kTextPlain,
                        );
                        if (data?.text != null) {
                          setState(() {
                            _tokenController.text = data!.text!.trim();
                          });
                        }
                      },
                    ),
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(12),
              ),
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
            style: FilledButton.styleFrom(
              padding: const EdgeInsets.symmetric(vertical: 14),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(12),
              ),
            ),
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
            label: Text(
              authState.isLoading ? '正在登录...' : '使用 Token 登录',
              style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600),
            ),
          ),
        ],
      ),
    );
  }
}
