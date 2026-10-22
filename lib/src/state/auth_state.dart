import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../native/api/auth.dart' as api_auth;

class AuthState {
  final api_auth.UserProfile? user;
  final bool isLoading;
  final String? errorMessage;
  final bool isGuestMode;

  const AuthState({
    this.user,
    this.isLoading = false,
    this.errorMessage,
    this.isGuestMode = false,
  });

  bool get isLoggedIn => user != null;
  bool get isAuthenticated => isLoggedIn;

  AuthState copyWith({
    api_auth.UserProfile? user,
    bool? isLoading,
    String? errorMessage,
    bool clearUser = false,
    bool? isGuestMode,
  }) {
    return AuthState(
      user: clearUser ? null : (user ?? this.user),
      isLoading: isLoading ?? this.isLoading,
      errorMessage: errorMessage,
      isGuestMode: isGuestMode ?? (clearUser ? false : this.isGuestMode),
    );
  }
}

class AuthNotifier extends Notifier<AuthState> {
  Future<void>? _initialization;
  static const _refreshTokenKey = 'pixeval_refresh_token';
  static const _pkceVerifierKey = 'pixeval_pkce_verifier';

  Future<String?> getSavedVerifier() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(_pkceVerifierKey);
  }

  Future<api_auth.PkceAuthParamsDto> createOAuthAuthorization() async {
    final parameters = await api_auth.createPkceAuthParams();
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_pkceVerifierKey, parameters.codeVerifier);
    return parameters;
  }

  Future<bool> completeOAuthCallback(Uri callback) async {
    return loginWithOAuthInput(callback.toString());
  }

  Future<bool> loginWithOAuthInput(
    String rawInput, [
    String? explicitVerifier,
  ]) async {
    await _initialization;
    if (!ref.mounted) return false;
    state = state.copyWith(isLoading: true, errorMessage: null);

    String code;
    try {
      final extracted = extractOAuthCode(rawInput);
      if (extracted == null || extracted.isEmpty) {
        state = state.copyWith(
          isLoading: false,
          errorMessage: '请输入有效授权回调链接或授权码',
        );
        return false;
      }
      code = extracted;
    } on FormatException catch (e) {
      state = state.copyWith(isLoading: false, errorMessage: e.message);
      return false;
    } catch (e) {
      state = state.copyWith(isLoading: false, errorMessage: '解析授权码失败: $e');
      return false;
    }

    final prefs = await SharedPreferences.getInstance();
    final verifier = explicitVerifier ?? prefs.getString(_pkceVerifierKey);
    if (verifier == null || verifier.isEmpty) {
      state = state.copyWith(
        isLoading: false,
        errorMessage: '未找到待处理的授权验证码 (Verifier)，请先点击【生成授权并在浏览器打开】发起授权。',
      );
      return false;
    }

    return loginWithCode(code, verifier);
  }

  void enterGuestMode() {
    state = state.copyWith(isGuestMode: true);
  }

  void exitGuestMode() {
    state = state.copyWith(isGuestMode: false);
  }

  @override
  AuthState build() {
    // Trigger async initialization on build
    _initialization = Future.microtask(_initSession);
    return const AuthState(isLoading: true);
  }

  Future<void> _initSession() async {
    state = state.copyWith(isLoading: true);
    try {
      final prefs = await SharedPreferences.getInstance();
      // Prefer the durable Rust session, including automatic token rotations.
      final token =
          await api_auth.getStoredRefreshToken() ??
          prefs.getString(_refreshTokenKey);
      if (!ref.mounted) return;
      if (token != null && token.isNotEmpty) {
        final authResp = await api_auth.loginByRefreshToken(
          refreshToken: token,
        );
        if (!ref.mounted) return;
        if (authResp.refreshToken.isNotEmpty &&
            authResp.refreshToken != token) {
          await prefs.setString(_refreshTokenKey, authResp.refreshToken);
        }
        state = state.copyWith(
          user: authResp.user,
          isLoading: false,
          isGuestMode: false,
        );
      } else {
        state = state.copyWith(isLoading: false);
      }
    } catch (e) {
      if (!ref.mounted) return;
      state = state.copyWith(isLoading: false, errorMessage: e.toString());
    }
  }

  Future<bool> loginWithRefreshToken(String refreshToken) async {
    await _initialization;
    if (!ref.mounted) return false;
    state = state.copyWith(isLoading: true, errorMessage: null);
    try {
      final authResp = await api_auth.loginByRefreshToken(
        refreshToken: refreshToken,
      );
      final prefs = await SharedPreferences.getInstance();
      final tokenToSave = authResp.refreshToken.isNotEmpty
          ? authResp.refreshToken
          : refreshToken;
      await prefs.setString(_refreshTokenKey, tokenToSave);
      state = state.copyWith(
        user: authResp.user,
        isLoading: false,
        isGuestMode: false,
      );
      return true;
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        errorMessage: formatAuthErrorMessage(e.toString()),
      );
      return false;
    }
  }

  Future<bool> loginWithCode(String code, String codeVerifier) async {
    await _initialization;
    if (!ref.mounted) return false;
    state = state.copyWith(isLoading: true, errorMessage: null);
    try {
      final authResp = await api_auth.loginByCode(
        code: code,
        codeVerifier: codeVerifier,
      );
      final prefs = await SharedPreferences.getInstance();
      if (authResp.refreshToken.isNotEmpty) {
        await prefs.setString(_refreshTokenKey, authResp.refreshToken);
      }
      await prefs.remove(_pkceVerifierKey);
      state = state.copyWith(
        user: authResp.user,
        isLoading: false,
        isGuestMode: false,
      );
      return true;
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        errorMessage: formatAuthErrorMessage(e.toString()),
      );
      return false;
    }
  }

  Future<void> refreshSession() async {
    try {
      final authResp = await api_auth.refreshSession();
      if (authResp.refreshToken.isNotEmpty) {
        final prefs = await SharedPreferences.getInstance();
        await prefs.setString(_refreshTokenKey, authResp.refreshToken);
      }
      state = state.copyWith(user: authResp.user);
    } catch (e) {
      state = state.copyWith(errorMessage: e.toString());
    }
  }

  Future<void> handleAuthInvalidated([String? reason]) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.remove(_refreshTokenKey);
      await api_auth.logout();
    } catch (_) {}
    state = state.copyWith(
      clearUser: true,
      isLoading: false,
      isGuestMode: false,
      errorMessage: reason != null
          ? 'Session expired: $reason'
          : 'Session invalidated',
    );
  }

  Future<void> logout() => performLogout();

  Future<void> performLogout() async {
    await api_auth.logout();
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_refreshTokenKey);
    await prefs.remove(_pkceVerifierKey);
    state = state.copyWith(
      clearUser: true,
      isLoading: false,
      isGuestMode: false,
    );
  }
}

final authProvider = NotifierProvider<AuthNotifier, AuthState>(
  AuthNotifier.new,
);

/// Decodes Unicode escapes and formats raw Pixiv OAuth errors into user-friendly messages.
String formatAuthErrorMessage(String error) {
  var decoded = error;
  if (decoded.contains(r'\u')) {
    decoded = decoded.replaceAllMapped(RegExp(r'\\u([0-9a-fA-F]{4})'), (match) {
      final hex = match.group(1)!;
      return String.fromCharCode(int.parse(hex, radix: 16));
    });
  }

  if (decoded.contains('invalid_request') ||
      decoded.contains('"code":918') ||
      decoded.contains('不正なリクエスト')) {
    return '授权请求无效 (invalid_request)：授权码已失效或与当前验证会话不匹配。\n💡 解决办法：若此前点击过【重新生成授权链接】，请在浏览器中重新登录并复制最新的跳转网址。';
  }
  if (decoded.contains('invalid_grant')) {
    return '授权码或 Refresh Token 无效或已过期，请重新发起网页授权登录。';
  }
  if (decoded.contains('有効期限が切れて')) {
    return '授权码已过期（超过5分钟），请重新点击【生成授权并在浏览器打开】完成登录。';
  }

  return decoded
      .replaceFirst('Exchange code error: Authentication error: ', '')
      .replaceFirst('Exchange code error: ', '')
      .replaceFirst('Login by refresh token failed: Authentication error: ', '')
      .replaceFirst('Login by refresh token failed: ', '')
      .trim();
}

/// Identifies whether the given text is a Pixiv OAuth authorization callback or code.
/// Explicitly excludes the authorization request / login page URL.
bool isPixivCallbackUrl(String raw) {
  final text = raw.trim();
  if (text.isEmpty) return false;

  // Never treat the login request URL as a callback
  if (text.contains('/web/v1/login') || text.contains('code_challenge=')) {
    return false;
  }

  // Matches callback URLs containing 'code=' and pixiv markers
  if (text.contains('code=') &&
      (text.contains('pixiv') || text.contains('app-api.pixiv.net'))) {
    return true;
  }

  // Matches custom scheme callback redirect
  if (text.startsWith('pixiv://account/login') ||
      text.startsWith('intent://account/login')) {
    return true;
  }

  return false;
}

/// Robustly extracts the authorization code from a callback URL, intent link, or raw string.
/// Throws [FormatException] if the input is an authorization login URL or contains no valid code.
String? extractOAuthCode(String rawInput) {
  final input = rawInput.trim();
  if (input.isEmpty) return null;

  // Detect mistakenly entered login URL
  if (input.contains('/web/v1/login') || input.contains('code_challenge=')) {
    throw const FormatException(
      '检测到您输入的是发起授权的登录链接（包含 code_challenge），而不是授权完成后的回调链接！\n请在浏览器中完成登录，若提示【无法打开网页】，请复制地址栏中包含 code= 的网址。',
    );
  }

  // 1. Extract from standard query parameters or fragments
  if (input.contains('code=')) {
    final uri = Uri.tryParse(input);
    if (uri != null && uri.queryParameters.containsKey('code')) {
      var c = uri.queryParameters['code']!;
      if (c.contains('#')) c = c.split('#').first;
      return Uri.decodeComponent(c).trim();
    }
    final match =
        RegExp(r'[?&#]code=([^&#\s]+)').firstMatch(input) ??
        RegExp(r'code=([^&#\s]+)').firstMatch(input);
    if (match != null) {
      return Uri.decodeComponent(match.group(1)!).trim();
    }
  }

  // 2. Reject URLs without a code parameter
  if (input.startsWith('http://') ||
      input.startsWith('https://') ||
      input.startsWith('pixiv://') ||
      input.startsWith('intent://')) {
    throw const FormatException(
      '该链接中未包含 Pixiv 授权码 (code=)。\n请确保在浏览器中完成授权，并复制跳转后的完整回调网址。',
    );
  }

  // 3. Assume raw code string (validate format: only url-safe chars)
  final cleanCode = input.replaceAll(RegExp(r'[\r\n\t\s]'), '');
  if (RegExp(r'^[A-Za-z0-9_-]+$').hasMatch(cleanCode)) {
    return cleanCode;
  }

  throw const FormatException('无法识别的授权码或回调链接格式，请确认复制的内容正确。');
}
