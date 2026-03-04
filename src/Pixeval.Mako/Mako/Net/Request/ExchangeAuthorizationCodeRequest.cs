// Copyright (c) Mako.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Mako.Net.Request;
// ReSharper disable MemberCanBeMadeStatic.Global
#pragma warning disable CA1822
public class ExchangeAuthorizationCodeRequest(string code, string codeVerifier, string redirectUri)
{
    [JsonPropertyName("code")]
    public string Code { get; } = code;

    [JsonPropertyName("code_verifier")]
    public string CodeVerifier { get; } = codeVerifier;

    [JsonPropertyName("redirect_uri")]
    public string RedirectUri { get; } = redirectUri;

    [JsonPropertyName("grant_type")]
    public string GrantType => "authorization_code";

    [JsonPropertyName("client_id")]
    public string ClientId => "MOBrBDS8blbauoSck0ZfDbtuzpyT";

    [JsonPropertyName("client_secret")]
    public string ClientSecret => "lsACyCD94FhDUtGTXi3QzcFE2uU1hqtDaKeqrdwj";

    [JsonPropertyName("include_policy")]
    public string IncludePolicy => "true";
}
#pragma warning restore CA1822
