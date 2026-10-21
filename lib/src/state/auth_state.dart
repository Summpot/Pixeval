import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../rust/api/auth.dart' as api_auth;

class AuthState {
  final api_auth.UserProfile? user;
  final bool isLoading;
  final String? errorMessage;

  const AuthState({this.user, this.isLoading = false, this.errorMessage});

  bool get isLoggedIn => user != null;
  bool get isAuthenticated => isLoggedIn;

  AuthState copyWith({
    api_auth.UserProfile? user,
    bool? isLoading,
    String? errorMessage,
    bool clearUser = false,
  }) {
    return AuthState(
      user: clearUser ? null : (user ?? this.user),
      isLoading: isLoading ?? this.isLoading,
      errorMessage: errorMessage,
    );
  }
}

class AuthNotifier extends Notifier<AuthState> {
  Future<void>? _initialization;
  static const _refreshTokenKey = 'pixeval_refresh_token';
  static const _pkceVerifierKey = 'pixeval_pkce_verifier';

  Future<api_auth.PkceAuthParamsDto> createOAuthAuthorization() async {
    final parameters = await api_auth.createPkceAuthParams();
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_pkceVerifierKey, parameters.codeVerifier);
    return parameters;
  }

  Future<bool> completeOAuthCallback(Uri callback) async {
    final code = callback.queryParameters['code'];
    final prefs = await SharedPreferences.getInstance();
    final verifier = prefs.getString(_pkceVerifierKey);
    if (code == null || code.isEmpty || verifier == null || verifier.isEmpty) {
      return false;
    }
    return loginWithCode(code, verifier);
  }

  @override
  AuthState build() {
    // Trigger async initialization on build
    _initialization = Future.microtask(_initSession);
    return const AuthState();
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
        state = state.copyWith(user: authResp.user, isLoading: false);
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
      state = state.copyWith(user: authResp.user, isLoading: false);
      return true;
    } catch (e) {
      state = state.copyWith(isLoading: false, errorMessage: e.toString());
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
      state = state.copyWith(user: authResp.user, isLoading: false);
      return true;
    } catch (e) {
      state = state.copyWith(isLoading: false, errorMessage: e.toString());
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
    state = state.copyWith(clearUser: true, isLoading: false);
  }
}

final authProvider = NotifierProvider<AuthNotifier, AuthState>(
  AuthNotifier.new,
);
