// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Native.Config;

namespace Pixeval.Tests;

[TestClass]
public sealed class ConfigEngineTest
{
    [TestMethod]
    public void MigrateYamlShouldNestLegacyFlatKeys()
    {
        using var engine = new ConfigEngine();

        const string legacyYaml = """
            ApplicationSettings:
              LimitFileCacheSize: true
              FileCacheSizeLimitInMegabytes: 2048
              HomePageRows: 4
            BrowsingExperienceSettings:
              IllustrationViewerAutoPlayInterval: 12
            DownloadSettings:
              IllustrationDownloadFormat: custom-format
            NetworkSettings:
              EnablePixivDomainFronting: true
              Proxy: http://127.0.0.1:7890
            """;

        var migrated = engine.MigrateYaml(legacyYaml);

        Assert.IsTrue(migrated.Contains("FileCache:"), "Should contain FileCache group");
        Assert.IsTrue(migrated.Contains("HomePage:"), "Should contain HomePage group");
        Assert.IsTrue(migrated.Contains("AutoPlay:"), "Should contain AutoPlay group");
        Assert.IsTrue(migrated.Contains("DownloadFormats:"), "Should contain DownloadFormats group");
        Assert.IsTrue(migrated.Contains("PixivDomainFronting:"), "Should contain PixivDomainFronting group");
        Assert.IsTrue(migrated.Contains("ProxySettings:"), "Should contain ProxySettings group");
    }

    [TestMethod]
    public void ValidateAndNormalizeYamlShouldSucceedForValidYaml()
    {
        using var engine = new ConfigEngine();

        const string yaml = """
            Theme: 1
            Language: zh-CN
            """;

        var normalized = engine.ValidateAndNormalizeYaml(yaml);
        Assert.IsTrue(normalized.Contains("Theme: 1"));
        Assert.IsTrue(normalized.Contains("Language: zh-CN"));
    }

    [TestMethod]
    public void SaveAndLoadFromFileShouldPersistAtomically()
    {
        using var engine = new ConfigEngine();
        var tempFile = Path.Combine(Path.GetTempPath(), "pixeval_test_config_" + Guid.NewGuid().ToString("N") + ".yaml");

        try
        {
            const string content = """
                AppVersion: 2.0.0
                StoragePath: D:\PixevalData
                """;

            engine.SaveToFile(tempFile, content);
            Assert.IsTrue(File.Exists(tempFile));

            var loaded = engine.LoadFromFile(tempFile);
            Assert.AreEqual(content.Trim().Replace("\r\n", "\n"), loaded.Trim().Replace("\r\n", "\n"));

            // Overwrite atomically
            const string updated = """
                AppVersion: 2.0.1
                StoragePath: D:\PixevalData\Updated
                """;

            engine.SaveToFile(tempFile, updated);
            var loadedUpdated = engine.LoadFromFile(tempFile);
            Assert.AreEqual(updated.Trim().Replace("\r\n", "\n"), loadedUpdated.Trim().Replace("\r\n", "\n"));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
