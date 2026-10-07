using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Native.Cache;

namespace Pixeval.Tests;

[TestClass]
public sealed class CacheEngineTest
{
    [TestMethod]
    public void CacheEnginePutGetRemoveShouldSucceed()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "pixeval_test_cache_" + Guid.NewGuid().ToString("N"));
        try
        {
            using var cache = new CacheEngine(tempDir, 8192, 4);

            Assert.IsNull(cache.Get("key1"));

            var data = Encoding.UTF8.GetBytes("hello from rust cache!");
            cache.Put("key1", data);

            var retrieved = cache.Get("key1");
            Assert.IsNotNull(retrieved);
            Assert.AreEqual("hello from rust cache!", Encoding.UTF8.GetString(retrieved));

            var stats = cache.Stats();
            Assert.AreEqual(1u, stats.EntryCount);
            Assert.AreEqual((ulong) ((data.Length + 7) & ~7), stats.TotalUsedBytes);

            Assert.IsTrue(cache.Remove("key1"));
            Assert.IsNull(cache.Get("key1"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [TestMethod]
    public void CacheEnginePersistenceAcrossRestartShouldWork()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "pixeval_test_restart_" + Guid.NewGuid().ToString("N"));
        try
        {
            var data = Encoding.UTF8.GetBytes("persistent cached content");
            using (var cache1 = new CacheEngine(tempDir, 8192, 4))
            {
                cache1.Put("restart_key", data);
                Assert.IsNotNull(cache1.Get("restart_key"));
            }

            // Reopen engine on the same directory
            using (var cache2 = new CacheEngine(tempDir, 8192, 4))
            {
                var restored = cache2.Get("restart_key");
                Assert.IsNotNull(restored, "Cached item should survive CacheEngine restart");
                Assert.AreEqual("persistent cached content", Encoding.UTF8.GetString(restored));
                var stats = cache2.Stats();
                Assert.AreEqual(1u, stats.EntryCount);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [TestMethod]
    public void CacheEngineExpansionAndPurgeCompactShouldWork()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "pixeval_test_compact_" + Guid.NewGuid().ToString("N"));
        try
        {
            // 64 bytes initial chunk to force expansion across chunks
            using var cache = new CacheEngine(tempDir, 64, 4);

            var d1 = new byte[32];
            Array.Fill(d1, (byte) 1);
            var d2 = new byte[32];
            Array.Fill(d2, (byte) 2);
            var d3 = new byte[32];
            Array.Fill(d3, (byte) 3);

            cache.Put("k1", d1);
            cache.Put("k2", d2);
            cache.Put("k3", d3);

            var stats = cache.Stats();
            Assert.IsTrue(stats.FileCount >= 2);
            Assert.AreEqual(3u, stats.EntryCount);

            var evicted = cache.PurgeCompact();
            Assert.IsTrue(evicted >= 1);

            var statsAfter = cache.Stats();
            Assert.IsTrue(statsAfter.EntryCount < 3);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [TestMethod]
    public void CacheEngineShouldPutAndReadStream()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "pixeval_test_cachetable_" + Guid.NewGuid().ToString("N"));
        try
        {
            using var cache = new CacheEngine(tempDir, 4096, 4);
            var payload = Encoding.UTF8.GetBytes("hello native cache!");
            Assert.IsTrue(cache.TryCache("k1", payload));

            Assert.IsTrue(cache.TryReadCache("k1", out var stream));
            Assert.IsNotNull(stream);
            using (stream)
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                Assert.AreEqual("hello native cache!", Encoding.UTF8.GetString(ms.ToArray()));
            }

            // Test TryCache with stream
            using (var writeStream = new MemoryStream(payload))
            {
                Assert.IsTrue(cache.TryCache("k2", writeStream));
            }
            Assert.IsTrue(cache.TryReadCache("k2", out var readStream2));
            Assert.IsNotNull(readStream2);
            readStream2.Dispose();

            Assert.IsTrue(cache.Remove("k1"));
            Assert.IsFalse(cache.TryReadCache("k1", out _));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [TestMethod]
    public void CacheEnginePurgeToSizeAndOverwriteShouldWork()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "pixeval_test_purgetosize_" + Guid.NewGuid().ToString("N"));
        try
        {
            using var cache = new CacheEngine(tempDir, 128, 4);

            var d1 = new byte[32];
            Array.Fill(d1, (byte) 1);
            var d2 = new byte[32];
            Array.Fill(d2, (byte) 2);
            var d3 = new byte[32];
            Array.Fill(d3, (byte) 3);

            cache.Put("k1", d1);
            cache.Put("k2", d2);
            cache.Put("k3", d3);

            var stats = cache.Stats();
            Assert.AreEqual(3u, stats.EntryCount);
            Assert.AreEqual(96ul, stats.TotalUsedBytes);

            // Overwrite k3 in place with 16 bytes
            var d3New = new byte[16];
            Array.Fill(d3New, (byte) 9);
            cache.Put("k3", d3New);

            var statsOverwritten = cache.Stats();
            Assert.AreEqual(3u, statsOverwritten.EntryCount);
            Assert.AreEqual(80ul, statsOverwritten.TotalUsedBytes);

            // Purge down to 40 bytes
            var evicted = cache.PurgeToSize(40);
            Assert.AreEqual(2ul, evicted);

            var statsAfter = cache.Stats();
            Assert.AreEqual(1u, statsAfter.EntryCount);
            Assert.AreEqual(16ul, statsAfter.TotalUsedBytes);
            Assert.IsNull(cache.Get("k1"));
            Assert.IsNull(cache.Get("k2"));
            var k3Retrieved = cache.Get("k3");
            Assert.IsNotNull(k3Retrieved);
            Assert.AreEqual(16, k3Retrieved.Length);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
