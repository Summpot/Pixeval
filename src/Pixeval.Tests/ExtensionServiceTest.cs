using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.AppManagement;
using Pixeval.Extensions.Common;
using Pixeval.Models.Extensions;
using Pixeval.Utilities;

namespace Pixeval.Tests;

[TestClass]
public sealed class ExtensionServiceTest
{
    [TestMethod]
    public void InstalledExtensionHostsShouldLoadAndExposeMetadata()
    {
        var logger = new FileLogger(Path.Combine(Path.GetTempPath(), nameof(Pixeval), nameof(ExtensionServiceTest)));
        using var service = new ExtensionService(logger, [], [], loadInstalledHosts: false);
        var failures = new List<string>();
        var loadedHosts = 0;
        var outdatedHosts = 0;

        foreach (var host in ExtensionService.EnumerateLocalExtensionHosts(AppInfo.ExtensionsFolder))
        {
            var result = service.TryLoadHostWithResult(
                host.LibraryPath,
                logger,
                out var model,
                out _,
                host.UninstallTargetRelativePath);
            if (result is ExtensionHostLoadResult.OutdatedSdk)
            {
                ++outdatedHosts;
                continue;
            }

            if (result is not ExtensionHostLoadResult.Loaded || model is null)
            {
                failures.Add($"{Path.GetFileName(host.LibraryPath)}: {result}");
                continue;
            }

            ++loadedHosts;
            AssertHostMetadata(model, host.LibraryPath);
            foreach (var extension in model.Extensions)
                AssertExtensionMetadata(extension, host.LibraryPath);
        }

        if (failures.Count > 0)
            Assert.Fail("Some extension hosts failed to load:" + Environment.NewLine +
                        string.Join(Environment.NewLine, failures));

        if (loadedHosts is 0)
            Assert.Inconclusive(outdatedHosts is 0
                ? $"No extension host native libraries were found in {AppInfo.ExtensionsFolder}."
                : $"Only outdated extension host native libraries were found in {AppInfo.ExtensionsFolder}.");
    }

    private static void AssertHostMetadata(ExtensionsHostModel model, string dll)
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(model.Name), $"{dll} host name is empty.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(model.Author), $"{model.Name} author is empty.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(model.Version), $"{model.Name} version is empty.");
        Assert.IsNotNull(model.Description, $"{model.Name} description is null.");
        Assert.IsNotNull(model.Extensions, $"{model.Name} extensions collection is null.");
        var icon = model.Host.GetIcon(out var iconCount);
        Assert.AreEqual(icon?.Length ?? 0, iconCount,
            $"{model.Name} icon count does not match the returned buffer length.");
        Assert.IsNotNull(model.Icon, $"{model.Name} icon control is null.");
        _ = model.Link?.ToString();
        _ = model.HelpLink?.ToString();
    }

    private static void AssertExtensionMetadata(IExtension extension, string dll)
    {
        Assert.IsNotNull(extension, $"{dll} returned a null extension.");

        switch (extension)
        {
            case IEntryExtension entry:
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Label), $"{dll} extension label is empty.");
                Assert.IsNotNull(entry.Description, $"{entry.Label} description is null.");
                break;
            default:
                Assert.IsNotNull(extension.GetType().FullName, $"{dll} extension type name is null.");
                break;
        }
    }

    [TestMethod]
    public void EnumerateLocalExtensionHostsShouldReturnEmptyForEmptyDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), nameof(Pixeval), Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);
        try
        {
            var hosts = ExtensionService.EnumerateLocalExtensionHosts(tempDir);
            Assert.AreEqual(0, System.Linq.Enumerable.Count(hosts));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [TestMethod]
    public void UninstallTargetResolutionAndCleanupViaPluginEngine()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), nameof(Pixeval), Guid.NewGuid().ToString("N"));
        var extensionsFolder = Path.Combine(tempDir, "Extensions");
        _ = Directory.CreateDirectory(extensionsFolder);
        try
        {
            using var engine = new Pixeval.Native.Plugin.PluginHostEngine("5.0.0");

            var directFile = Path.Combine(extensionsFolder, "plugin.dll");
            File.WriteAllText(directFile, "test");
            var rel1 = engine.GetUninstallTargetRelativePath(directFile, extensionsFolder);
            Assert.AreEqual("plugin.dll", rel1);

            var subDir = Path.Combine(extensionsFolder, "SubPlugin");
            _ = Directory.CreateDirectory(subDir);
            var subFile = Path.Combine(subDir, "plugin.dll");
            File.WriteAllText(subFile, "test");
            var rel2 = engine.GetUninstallTargetRelativePath(subFile, extensionsFolder);
            Assert.AreEqual("SubPlugin", rel2);

            var failed = engine.CleanPendingUninstalls(["plugin.dll", "SubPlugin"], extensionsFolder);
            Assert.AreEqual(0, failed.Count);
            Assert.IsFalse(File.Exists(directFile));
            Assert.IsFalse(Directory.Exists(subDir));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [TestMethod]
    public void VerifyAndInstallPluginShouldRejectInvalidFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), nameof(Pixeval), Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);
        try
        {
            using var engine = new Pixeval.Native.Plugin.PluginHostEngine("5.0.0");
            var fakePackage = Path.Combine(tempDir, "fake.unknown");
            File.WriteAllText(fakePackage, "not a plugin");

            var threw = false;
            try
            {
                _ = engine.VerifyAndInstallPlugin(fakePackage, tempDir);
            }
            catch (Pixeval.Native.Plugin.PluginError)
            {
                threw = true;
            }
            Assert.IsTrue(threw, "Expected PluginError when verifying invalid package");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
