using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.AppManagement;
using Pixeval.Extensions.Common;
using Pixeval.Models.Extensions;
using Pixeval.Utilities;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class NativeExtensionLifetimeTest
{
    [TestMethod]
    public void UnloadKeepsLiveInterfacesCallableAndEventuallyReleasesTheLibrary()
    {
        var fixture = Environment.GetEnvironmentVariable("PIXEVAL_TEST_EXTENSION_FIXTURE");
        if (string.IsNullOrEmpty(fixture))
            Assert.Inconclusive("Set PIXEVAL_TEST_EXTENSION_FIXTURE to the pinned native SDK demo library.");
        var path = Path.Combine(AppInfo.TempFolder, Guid.NewGuid().ToString("N") + Path.GetExtension(fixture));
        File.Copy(fixture, path);
        var (library, host) = LoadAndUnload(path);
        for (var attempt = 0; attempt < 4 && (library.IsAlive || host.IsAlive); attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Assert.IsFalse(host.IsAlive, "The native host wrapper must be collectable after unload.");
        Assert.IsFalse(library.IsAlive, "The library lifetime must not be pinned for the entire process.");
        // On Windows, deleting a still-loaded DLL fails; this also checks actual release.
        File.Delete(path);
        Assert.IsFalse(File.Exists(path));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Library, WeakReference Host) LoadAndUnload(string path)
    {
        var logger = new FileLogger(AppInfo.LogsFolder);
        using var service = new ExtensionService(logger, [], [], loadInstalledHosts: false);
        Assert.AreEqual(ExtensionHostLoadResult.Loaded,
            service.TryLoadHostWithResult(path, logger, out var model, out _));
        Assert.IsNotNull(model);
        var library = new WeakReference(model.Handle);
        var host = new WeakReference(model.Host);
        var retained = model.Extensions.OfType<IEntryExtension>().First();
        var label = retained.Label;
        service.UnloadHost(model);
        model.Dispose();
        Assert.IsEmpty(service.HostModels);
        Assert.IsEmpty(service.SettingsGroups);
        Assert.IsEmpty(model.Extensions);
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = model.Host);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Assert.IsTrue(library.IsAlive, "A live extension interface must retain its native implementation.");
        Assert.AreEqual(label, retained.Label);
        GC.KeepAlive(retained);
        return (library, host);
    }
}
