// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Native.Maho;

namespace Pixeval.Tests;

[TestClass]
public sealed class MahoNetworkTest
{
    private static byte[] MakeSyntheticClientHello(string hostname)
    {
        var hostBytes = System.Text.Encoding.UTF8.GetBytes(hostname);
        var hostLen = hostBytes.Length;

        // SNI extension content
        var sniListLen = 3 + hostLen;
        var sniData = new List<byte>
        {
            (byte) (sniListLen >> 8), (byte) sniListLen,
            0x00, // HostName type
            (byte) (hostLen >> 8), (byte) hostLen
        };
        sniData.AddRange(hostBytes);

        // Extension header
        var ext = new List<byte>
        {
            0x00, 0x00, // SNI extension id = 0
            (byte) (sniData.Count >> 8), (byte) sniData.Count
        };
        ext.AddRange(sniData);

        // Handshake body
        var handshake = new List<byte>
        {
            0x01, // ClientHello
            0x00, 0x00, 0x00, // length placeholder
            0x03, 0x03 // ClientVersion TLS 1.2
        };
        for (var i = 0; i < 32; i++)
            handshake.Add(0x42); // Random
        handshake.Add(0x00); // Session ID len = 0
        handshake.AddRange([0x00, 0x02, 0x13, 0x01]); // Cipher suites
        handshake.AddRange([0x01, 0x00]); // Compression methods
        handshake.AddRange([(byte) (ext.Count >> 8), (byte) ext.Count]);
        handshake.AddRange(ext);

        var hsBodyLen = handshake.Count - 4;
        handshake[1] = (byte) ((hsBodyLen >> 16) & 0xFF);
        handshake[2] = (byte) ((hsBodyLen >> 8) & 0xFF);
        handshake[3] = (byte) (hsBodyLen & 0xFF);

        // TLS Record header
        var record = new List<byte>
        {
            0x16, // Handshake
            0x03, 0x01, // TLS 1.0 record version
            (byte) (handshake.Count >> 8), (byte) handshake.Count
        };
        record.AddRange(handshake);

        return [.. record];
    }

    [TestMethod]
    public void CoreLocateServerNameShouldLocateHostname()
    {
        var packet = MakeSyntheticClientHello("app-api.pixiv.net");
        var located = PixevalMahoMethods.LocateServerName([.. packet]);

        Assert.AreEqual("app-api.pixiv.net", located);
    }

    [TestMethod]
    public void CoreSplitClientHelloShouldFragmentCorrectly()
    {
        var packet = MakeSyntheticClientHello("oauth.secure.pixiv.net");
        var fragments = PixevalMahoMethods.SplitClientHello([.. packet]);

        Assert.IsNotNull(fragments);
        Assert.AreEqual(3, fragments.Count);

        foreach (var frag in fragments)
        {
            Assert.IsTrue(frag.Length >= 5);
            Assert.AreEqual((byte) 0x16, frag[0]); // Handshake
            Assert.AreEqual((byte) 0x03, frag[1]);
            Assert.AreEqual((byte) 0x09, frag[2]); // Modified minor version for firewall evasion
            var payloadLen = (frag[3] << 8) | frag[4];
            Assert.AreEqual(frag.Length - 5, payloadLen);
        }
    }

    [TestMethod]
    public void NativeDnsResolverShouldMapAndClear()
    {
        using var resolver = new DnsResolver();
        resolver.SetMapping("custom.pixiv.net", ["1.2.3.4", "5.6.7.8"]);

        var ips = resolver.GetMapping("custom.pixiv.net");
        Assert.AreEqual(2, ips.Count);
        Assert.AreEqual("1.2.3.4", ips[0]);
        Assert.AreEqual("5.6.7.8", ips[1]);

        resolver.Clear();
        var empty = resolver.GetMapping("custom.pixiv.net");
        Assert.AreEqual(0, empty.Count);
    }

    [TestMethod]
    public async System.Threading.Tasks.Task NativeDnsResolverShouldResolveAsync()
    {
        using var resolver = new DnsResolver();
        resolver.SetMapping("override.pixiv.net", ["127.0.0.1"]);

        var ips = await resolver.ResolveAsync("override.pixiv.net");
        Assert.AreEqual(1, ips.Count);
        Assert.AreEqual("127.0.0.1", ips[0]);
    }

    [TestMethod]
    public void MahoClientHelloProcessorShouldDetectAndEmitFragments()
    {
        using var processor = new MahoClientHelloProcessor();
        Assert.IsFalse(processor.IsCompleted());

        var packet = MakeSyntheticClientHello("app-api.pixiv.net");
        var chunk1 = packet[..20];
        var chunk2 = packet[20..];

        var res1 = processor.ProcessChunk([.. chunk1]);
        Assert.IsTrue(res1.IsHandshake);
        Assert.AreEqual(0, res1.Fragments.Count);
        Assert.IsFalse(res1.IsCompleted);

        var res2 = processor.ProcessChunk([.. chunk2]);
        Assert.IsTrue(res2.IsHandshake);
        Assert.AreEqual(3, res2.Fragments.Count);
        Assert.IsTrue(res2.IsCompleted);
        Assert.IsTrue(processor.IsCompleted());
    }

    [TestMethod]
    public void CoreIsPixivHostShouldIdentifyDomains()
    {
        Assert.IsTrue(PixevalMahoMethods.IsPixivHost("i.pximg.net"));
        Assert.IsTrue(PixevalMahoMethods.IsPixivHost("s.pximg.net"));
        Assert.IsTrue(PixevalMahoMethods.IsPixivHost("app-api.pixiv.net"));
        Assert.IsTrue(PixevalMahoMethods.IsPixivHost("pixiv.net"));
        Assert.IsTrue(PixevalMahoMethods.IsPixivHost("pximg.net"));

        Assert.IsFalse(PixevalMahoMethods.IsPixivHost("127.0.0.1"));
        Assert.IsFalse(PixevalMahoMethods.IsPixivHost("github.com"));
        Assert.IsFalse(PixevalMahoMethods.IsPixivHost("example.com"));
    }

    [TestMethod]
    public void MahoClientShouldInitializeAndUpdateOptions()
    {
        var options = new MahoClientOptions(
            DomainFrontingEnabled: true,
            SplitDelayMs: 50,
            HostIps: new Dictionary<string, List<string>>
            {
                ["test.pixiv.net"] = ["1.2.3.4"]
            },
            ProxyUrl: "http://127.0.0.1:7890");

        using var client = new MahoClient(options);
        Assert.IsNotNull(client);

        var updated = new MahoClientOptions(
            DomainFrontingEnabled: false,
            SplitDelayMs: 0,
            HostIps: new Dictionary<string, List<string>>(),
            ProxyUrl: null);

        client.UpdateOptions(updated);
    }
}
