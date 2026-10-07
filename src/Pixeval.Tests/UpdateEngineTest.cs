// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.AppManagement;
using Pixeval.AppManagement.Settings;
using Pixeval.Native.Update;

namespace Pixeval.Tests;

[TestClass]
public class UpdateEngineTest
{
    private string _testDir = null!;

    [TestInitialize]
    public void Setup()
    {
        var targetTmp = Path.Combine(AppContext.BaseDirectory, "test_tmp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(targetTmp);
        _testDir = targetTmp;
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, true);
            }
            catch
            {
                // ignore cleanup errors
            }
        }
    }

    [TestMethod]
    public void UpdatePingShouldReturnPong()
    {
        var ping = PixevalUpdateMethods.UpdatePing();
        Assert.AreEqual("update_pong", ping);
    }

    [TestMethod]
    public void CompareVersionsShouldEvaluateCorrectStates()
    {
        using var engine = new UpdateEngine(null);

        Assert.AreEqual(UpdateState.UpToDate, engine.CompareVersions("5.0.13.0", "5.0.13"));
        Assert.AreEqual(UpdateState.BuildUpdate, engine.CompareVersions("5.0.13.0", "5.0.14"));
        Assert.AreEqual(UpdateState.MinorUpdate, engine.CompareVersions("5.0.13", "5.1.0"));
        Assert.AreEqual(UpdateState.MajorUpdate, engine.CompareVersions("5.0.13", "6.0.0"));
        Assert.AreEqual(UpdateState.Insider, engine.CompareVersions("5.0.14", "5.0.13"));
        Assert.AreEqual(UpdateState.Insider, engine.CompareVersions("6.0.0", "5.9.9"));
        Assert.AreEqual(UpdateState.Unknown, engine.CompareVersions("invalid-ver", "5.0.13"));
    }

    [TestMethod]
    public void VerifyFileSha256ShouldValidateChecksum()
    {
        using var engine = new UpdateEngine(null);

        var filePath = Path.Combine(_testDir, "test_sha256.txt");
        var content = "pixeval native update engine test content"u8.ToArray();
        File.WriteAllBytes(filePath, content);

        var expectedSha256 = Convert.ToHexStringLower(SHA256.HashData(content));

        Assert.IsTrue(engine.VerifyFileSha256(filePath, expectedSha256));
        Assert.IsFalse(engine.VerifyFileSha256(filePath, "0000000000000000000000000000000000000000000000000000000000000000"));

        var computed = engine.ComputeFileSha256(filePath);
        Assert.AreEqual(expectedSha256, computed);
    }

    [TestMethod]
    public void AppReleaseRecordShouldExposePropertiesAndSort()
    {
        var rel1 = new AppRelease(
            Version: "5.0.12",
            TagName: "v5.0.12",
            Title: "Pixeval 5.0.12",
            ReleaseNotes: "Notes 12",
            PublishedAt: null,
            HtmlUrl: "https://github.com/Pixeval/Pixeval/releases/tag/v5.0.12",
            IsPrerelease: false,
            Assets: []);

        var rel2 = new AppRelease(
            Version: "5.0.13",
            TagName: "v5.0.13",
            Title: "Pixeval 5.0.13",
            ReleaseNotes: "Notes 13",
            PublishedAt: null,
            HtmlUrl: "https://github.com/Pixeval/Pixeval/releases/tag/v5.0.13",
            IsPrerelease: false,
            Assets: []);

        Assert.AreEqual(new Version(5, 0, 12), rel1.ParsedVersion);
        Assert.AreEqual(new Version(5, 0, 13), rel2.ParsedVersion);
        Assert.AreEqual("Notes 12", rel1.ReleaseNote);
        Assert.IsNotNull(rel1.ReleaseUri);

        Assert.IsTrue(rel2.CompareTo(rel1) > 0);
        Assert.IsTrue(rel1.CompareTo(rel2) < 0);
    }

    [TestMethod]
    public void VersioningCompareUpdateStateShouldForwardToEngine()
    {
        var versioning = new Versioning();
        var state = versioning.CompareUpdateState(new Version(5, 0, 13, 0), new Version(5, 0, 14, 0));
        Assert.AreEqual(UpdateState.BuildUpdate, state);

        var upToDateState = versioning.CompareUpdateState(new Version(5, 0, 13, 0), new Version(5, 0, 13, 0));
        Assert.AreEqual(UpdateState.UpToDate, upToDateState);
    }

    [TestMethod]
    public void CreateEngineFromNetworkSettingsShouldSucceed()
    {
        var settings = new NetworkSettingsGroup();
        using var engine = UpdateEngine.CreateFromSettings(settings);
        Assert.IsNotNull(engine);
    }
}
