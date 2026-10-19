// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

#pragma warning disable CS0436

using UniFFISharp;

namespace Pixeval.Download;

public static partial class PixevalDownloadMethods
{
    [PreferUniFFI]
    public static partial string ReduceMetaPath(string raw, MacroContext ctx);

    [PreferUniFFI]
    public static partial MacroAnalysisResult AnalyzeMetaPath(string text);
}
