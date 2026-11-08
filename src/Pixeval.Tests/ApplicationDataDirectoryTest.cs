using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Services.Core;
using Pixeval.Config;
using Pixeval.Utilities;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ApplicationDataDirectoryTest
{
    [TestMethod]
    public void UnconfiguredHostKeepsDefaultPathSelection() =>
        Assert.IsNull(ApplicationDataDirectory.ValidateOverride(null));

    [TestMethod]
    [DataRow("")]
    [DataRow("relative/data")]
    [DataRow(42)]
    public void InvalidOverrideFailsInsteadOfUsingPersonalData(object value) =>
        Assert.ThrowsExactly<ArgumentException>(() => ApplicationDataDirectory.ValidateOverride(value));

    [TestMethod]
    public void SettingsRoundTripStaysInsideTheTestHost()
    {
        Assert.AreEqual(TestApplicationData.DirectoryPath, AppInfo.ApplicationFolderPath);
        foreach (var path in new[] { AppInfo.SettingsFolder, AppInfo.CacheFolder, AppInfo.LogsFolder,
                     AppInfo.TempFolder, AppInfo.ExtensionsFolder, AppInfo.DatabaseFilePath })
            Assert.StartsWith(TestApplicationData.DirectoryPath + Path.DirectorySeparatorChar, path);

        var settings = new AppSettings();
        settings.ApplicationSettings.HomePage.HomePageRows = 7;
        AppInfo.SaveAppSettings(settings);
        var restored = AppInfo.LoadAppSettings(new FileLogger(AppInfo.LogsFolder));

        Assert.IsNotNull(restored);
        Assert.AreEqual(7, restored.ApplicationSettings.HomePage.HomePageRows);
        Assert.IsTrue(File.Exists(Path.Combine(AppInfo.SettingsFolder, "settings.yaml")));
    }
}
