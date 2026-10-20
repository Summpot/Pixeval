// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.IO;
using System.Linq;

namespace Pixeval.Common.IO;

public static partial class IoHelper
{
    public static string InvalidPathChars { get; } = @"*?""|" + new string(Path.GetInvalidPathChars());

    public static string NormalizePath(string path)
    {
        return Path.GetFullPath(InvalidPathChars.Aggregate(path, (s, c) => s.Replace(c.ToString(), ""))).TrimEnd('.');
    }
}
