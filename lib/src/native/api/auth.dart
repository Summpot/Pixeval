import '../pixeval.g.dart';
export '../pixeval.g.dart' show UserProfile, AuthResponse, PkceAuthParamsDto;

import '../native_api.dart';

Future<AuthResponse> loginByCode({
  required String code,
  required String codeVerifier,
}) =>
    NativeApi.instance.api.loginByCode(code: code, codeVerifier: codeVerifier);

Future<AuthResponse> loginByRefreshToken({required String refreshToken}) =>
    NativeApi.instance.api.loginByRefreshToken(refreshToken: refreshToken);

Future<AuthResponse> refreshSession() =>
    NativeApi.instance.api.refreshSession();

Future<UserProfile?> getCurrentUser() =>
    NativeApi.instance.api.getCurrentUser();

Future<String?> getRefreshToken() => NativeApi.instance.api.getRefreshToken();

Future<void> logout() => NativeApi.instance.api.logout();

Future<String?> getStoredRefreshToken() =>
    NativeApi.instance.api.getStoredRefreshToken();

Future<PkceAuthParamsDto> createPkceAuthParams() =>
    NativeApi.instance.api.createPkceAuthParams();
