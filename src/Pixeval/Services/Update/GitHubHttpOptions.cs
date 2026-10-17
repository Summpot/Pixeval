// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.ObjectModel;
using System.Net;
using Pixeval.Config;

namespace Pixeval.Services.Update;

public static class GitHubHttpOptions
{
    public const string Host = "github.com";

    public const string ApiHost = "api.github.com";

    public const string AvatarHost = "avatars.githubusercontent.com";

    public const string UserContentHost = "*.githubusercontent.com";

    public const string AssetsHost = "github.githubassets.com";

    public const string CodeloadHost = "codeload.github.com";

    public static bool IsGitHubHost(string host) => Pixeval.Update.PixevalUpdateMethods.IsGithubHost(host);
}
