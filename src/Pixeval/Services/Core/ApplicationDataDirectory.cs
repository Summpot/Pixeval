using System;
using System.IO;

namespace Pixeval.Services.Core;

internal static class ApplicationDataDirectory
{
    // Test and UI verification hosts set this before touching AppInfo. Ordinary launches
    // keep the existing desktop/Store paths and never import data into an isolated host.
    internal const string OverrideKey = "Pixeval.ApplicationDataDirectory";

    internal static string? Override => ValidateOverride(AppContext.GetData(OverrideKey));

    internal static string? ValidateOverride(object? value) => value switch
    {
        null => null,
        string path when Path.IsPathFullyQualified(path) => Path.GetFullPath(path),
        _ => throw new ArgumentException($"{OverrideKey} must be an absolute directory path.", nameof(value))
    };
}
