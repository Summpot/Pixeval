// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Net;

namespace Pixeval.AppManagement;

public static class PixivLoginActivationHub
{
    public const string CallbackScheme = "pixiv";

    public const string CallbackHost = "account";

    public const string CallbackPath = "/login";

    public const string CallbackUri = "pixiv://account/login";

    private static readonly object _Sync = new();

    private static readonly Queue<Uri> _PendingUris = [];

    public static event Action<Uri>? Activated;

    public static bool IsCallbackUri(Uri uri) =>
        uri.IsAbsoluteUri
        && uri.Scheme.Equals(CallbackScheme, StringComparison.OrdinalIgnoreCase)
        && uri.Host.Equals(CallbackHost, StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.Equals(CallbackPath, StringComparison.OrdinalIgnoreCase);

    public static bool TryCreateCallbackUri(string value, out Uri uri)
    {
        if (Uri.TryCreate(value.Trim().Trim('"'), UriKind.Absolute, out uri!)
            && IsCallbackUri(uri))
            return true;

        uri = null!;
        return false;
    }

    public static void Submit(Uri uri)
    {
        if (!IsCallbackUri(uri))
            return;

        Action<Uri>? handler;
        lock (_Sync)
        {
            handler = Activated;
            if (handler is null)
                _PendingUris.Enqueue(uri);
        }

        handler?.Invoke(uri);
    }

    public static IReadOnlyList<Uri> DrainPendingUris()
    {
        lock (_Sync)
        {
            if (_PendingUris.Count is 0)
                return [];

            var uris = _PendingUris.ToArray();
            _PendingUris.Clear();
            return uris;
        }
    }

    public static bool TryExtractCode(string input, out string code)
    {
        var trimmed = input.Trim();
        if (trimmed.Length is 0)
        {
            code = "";
            return false;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            code = trimmed;
            return true;
        }

        if (TryExtractCode(uri, 4, out code))
            return true;

        code = "";
        return false;
    }

    private static bool TryExtractCode(Uri uri, int depth, out string code)
    {
        if (depth is 0)
        {
            code = "";
            return false;
        }

        foreach (var (key, value) in EnumerateQuery(uri))
        {
            if (key.Equals("code", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(value))
            {
                code = value;
                return true;
            }
        }

        foreach (var (key, value) in EnumerateQuery(uri))
        {
            if (key is not ("return_to" or "redirect" or "redirect_uri")
                || !Uri.TryCreate(value, UriKind.Absolute, out var nestedUri))
                continue;

            if (TryExtractCode(nestedUri, depth - 1, out code))
                return true;
        }

        code = "";
        return false;
    }

    private static IEnumerable<(string Key, string Value)> EnumerateQuery(Uri uri)
    {
        var query = uri.Query;
        if (string.IsNullOrEmpty(query))
            yield break;

        foreach (var part in query.AsSpan(1).ToString().Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equalsIndex = part.IndexOf('=');
            var key = equalsIndex < 0 ? part : part[..equalsIndex];
            var value = equalsIndex < 0 ? "" : part[(equalsIndex + 1)..];
            yield return (WebUtility.UrlDecode(key), WebUtility.UrlDecode(value));
        }
    }
}
