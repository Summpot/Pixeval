using System;
using System.IO;
using System.Runtime.CompilerServices;
using Pixeval.AppManagement;

namespace Pixeval.Tests;

internal static class TestApplicationData
{
    internal static string DirectoryPath { get; } = Path.Combine(AppContext.BaseDirectory,
        "TestResults", "ApplicationData", Guid.NewGuid().ToString("N"));

    [ModuleInitializer]
    internal static void Initialize()
    {
        // Run before test discovery or any AppInfo static initializer. Keep the isolated
        // files with test artifacts for failure diagnosis; never discover personal extensions.
        AppContext.SetData(ApplicationDataDirectory.OverrideKey, DirectoryPath);
        // An explicitly supplied, trusted native fixture is copied into this run only.
        // A missing fixture is an error, never a fallback to personal extensions.
        if (Environment.GetEnvironmentVariable("PIXEVAL_TEST_EXTENSION_FIXTURE") is { Length: > 0 } fixture)
        {
            if (!Path.IsPathFullyQualified(fixture) || !File.Exists(fixture))
                throw new IOException("PIXEVAL_TEST_EXTENSION_FIXTURE must name an existing absolute native library path.");
            File.Copy(fixture, Path.Combine(AppInfo.ExtensionsFolder, Path.GetFileName(fixture)));
        }
    }
}
