using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Mako;
using Mako.Net.EndPoints;
using Mako.Net.Request;
using Microsoft.Extensions.DependencyInjection;

namespace Pixeval.AppManagement;

public sealed class PixivBrowserLoginService
{
    private const string LoginUrl = "https://app-api.pixiv.net/web/v1/login";

    public const string RedirectUri = "https://app-api.pixiv.net/web/v1/users/auth/pixiv/callback";

    public const string CallbackScheme = "pixiv";

    private readonly object _lock = new();

    private string? _pendingCodeVerifier;

    private DateTimeOffset _pendingCreatedAt;

    public Uri CreateLoginUri()
    {
        var codeVerifier = GenerateCodeVerifier();
        var challenge = GenerateCodeChallenge(codeVerifier);

        lock (_lock)
        {
            _pendingCodeVerifier = codeVerifier;
            _pendingCreatedAt = DateTimeOffset.UtcNow;
        }

        var query = string.Join("&",
            $"code_challenge={Uri.EscapeDataString(challenge)}",
            "code_challenge_method=S256",
            "client=pixiv-android");

        return new UriBuilder(LoginUrl)
        {
            Query = query
        }.Uri;
    }

    public bool IsPixivCallbackUri(Uri uri)
    {
        if (!string.Equals(uri.Scheme, CallbackScheme, StringComparison.OrdinalIgnoreCase))
            return false;

        return TryExtractAuthorizationCode(uri, out _);
    }

    public async Task<string?> TryExchangeRefreshTokenAsync(MakoClient makoClient, Uri callbackUri)
    {
        if (!TryExtractAuthorizationCode(callbackUri, out var authorizationCode))
            return null;

        string? pendingCodeVerifier;
        DateTimeOffset pendingCreatedAt;

        lock (_lock)
        {
            pendingCodeVerifier = _pendingCodeVerifier;
            pendingCreatedAt = _pendingCreatedAt;
        }

        if (string.IsNullOrWhiteSpace(pendingCodeVerifier))
            return null;

        if (DateTimeOffset.UtcNow - pendingCreatedAt > TimeSpan.FromMinutes(10))
        {
            ClearPendingSession();
            return null;
        }

        var token = await makoClient.Provider
            .GetRequiredService<IAuthEndPoint>()
            .ExchangeAuthorizationCodeAsync(new ExchangeAuthorizationCodeRequest(authorizationCode, pendingCodeVerifier, RedirectUri))
            .ConfigureAwait(false);

        ClearPendingSession();
        return token.RefreshToken;
    }

    public bool TryExtractAuthorizationCode(Uri callbackUri, out string code)
    {
        return TryExtractAuthorizationCode(callbackUri.ToString(), out code);
    }

    public static bool TryExtractAuthorizationCode(string text, out string code)
    {
        code = string.Empty;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
            return false;

        return TryExtractAuthorizationCode(uri, out code, 0);
    }

    private static bool TryExtractAuthorizationCode(Uri uri, out string code, int depth)
    {
        code = string.Empty;

        if (depth >= 4)
            return false;

        var queryPairs = ParseQuery(uri.Query).ToArray();

        var directCode = queryPairs.FirstOrDefault(pair =>
            string.Equals(pair.Key, "code", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(directCode.Value))
        {
            code = directCode.Value;
            return true;
        }

        foreach (var (key, value) in queryPairs)
        {
            if (!IsNestedUriParameter(key) || string.IsNullOrWhiteSpace(value))
                continue;

            if (!Uri.TryCreate(value, UriKind.Absolute, out var nestedUri))
                continue;

            if (TryExtractAuthorizationCode(nestedUri, out code, depth + 1))
                return true;
        }

        return false;
    }

    private static bool IsNestedUriParameter(string key)
    {
        return string.Equals(key, "redirect_uri", StringComparison.OrdinalIgnoreCase)
               || string.Equals(key, "redirectUrl", StringComparison.OrdinalIgnoreCase)
               || string.Equals(key, "return_to", StringComparison.OrdinalIgnoreCase)
               || string.Equals(key, "target_url", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<(string Key, string Value)> ParseQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            yield break;

        foreach (var segment in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var splitIndex = segment.IndexOf('=');
            if (splitIndex < 0)
            {
                var keyOnly = Uri.UnescapeDataString(segment);
                yield return (keyOnly, string.Empty);
                continue;
            }

            var key = Uri.UnescapeDataString(segment[..splitIndex]);
            var value = Uri.UnescapeDataString(segment[(splitIndex + 1)..]);
            yield return (key, value);
        }
    }

    private static string GenerateCodeVerifier()
    {
        return ToBase64Url(RandomNumberGenerator.GetBytes(32));
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(codeVerifier));
        return ToBase64Url(bytes);
    }

    private static string ToBase64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private void ClearPendingSession()
    {
        lock (_lock)
        {
            _pendingCodeVerifier = null;
            _pendingCreatedAt = default;
        }
    }
}
