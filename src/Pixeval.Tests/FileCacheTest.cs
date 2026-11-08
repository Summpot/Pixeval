using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Utilities.IO.Caching;

namespace Pixeval.Tests;

[TestClass]
public sealed class FileCacheTest
{
    [TestMethod]
    public async Task InvalidatingOneImagePreservesOtherEntriesAndOpenReaders()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Pixeval-cache-retry-" + Guid.NewGuid().ToString("N"));
        try
        {
            var cache = new FileCache(directory);
            using var corrupt = new MemoryStream([1, 2, 3, 4]);
            using var other = new MemoryStream([5, 6, 7]);
            Assert.AreEqual(FileCacheWriteResult.Success, cache.TryCache("failed-image", corrupt, null));
            Assert.AreEqual(FileCacheWriteResult.Success, cache.TryCache("other-image", other, null));
            await cache.EnforceSizeLimitAsync(20);
            using (var reader = cache.TryOpen("failed-image"))
            {
                Assert.IsNotNull(reader);
                Assert.IsTrue(cache.TryRemove("failed-image"));
                Assert.IsNull(cache.TryOpen("failed-image"));
                Assert.AreEqual(1, reader.ReadByte(), "Existing readers retain their handle after eviction.");
            }
            using var replacement = new MemoryStream([8, 9]);
            Assert.AreEqual(FileCacheWriteResult.Success, cache.TryCache("failed-image", replacement, null));
            await cache.EnforceSizeLimitAsync(5);
            using var recovered = cache.TryOpen("failed-image");
            using var retained = cache.TryOpen("other-image");
            Assert.IsNotNull(recovered);
            Assert.IsNotNull(retained, "Size accounting must not count the removed four bytes after recovery.");
            Assert.AreEqual(8, recovered.ReadByte());
            Assert.AreEqual(5, retained.ReadByte());
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public async Task UnavailableCacheDirectoryShouldDegradeToFailure()
    {
        var cachePath = Path.GetTempFileName();
        try
        {
            var cache = new FileCache(cachePath);
            using var source = new MemoryStream([1, 2, 3]);

            Assert.IsNull(cache.TryOpen("key"));
            Assert.AreEqual(FileCacheWriteResult.Failed, cache.TryCache("key", source, null));

            await cache.EnforceSizeLimitAsync(1);
            await cache.PurgeAsync();
        }
        finally
        {
            File.Delete(cachePath);
        }
    }
}
