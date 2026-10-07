// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Native.Cache;
using Pixeval.Native.Download;
using Pixeval.Native.Mako;
using Pixeval.Native.Mcp;
using Pixeval.Native.Plugin;
using Pixeval.Native.Storage;
using Pixeval.Native.Subscription;

namespace Pixeval.Tests;

[TestClass]
public sealed class McpServerTest
{
    private sealed class MockSessionBridge : IMcpSessionBridge
    {
        public McpSessionUserInfo? GetCurrentUser() =>
            new("1001", "TestMcpUser", "test_mcp_account");

        public string GetHelpDocument(string? topic) =>
            $"Help text for {topic ?? "default"}";

        public void OnDownloadMacroChanged(string macroText) { }

        public void LogEvent(string level, string message) { }
    }

    [TestMethod]
    public void PluginHostEngineLifecycleAndState()
    {
        using var engine = new PluginHostEngine("5.0.0");
        Assert.AreEqual("5.0.0", engine.CurrentSdkVersion());

        var meta = new PluginMetadata(
            "test-plugin-id",
            "UnitTest Plugin",
            "Pixeval Team",
            "1.0.0",
            "Test description",
            "5.0.0",
            "",
            [],
            true
        );

        engine.RegisterMetadata(meta);
        var plugins = engine.GetLoadedPlugins();
        Assert.AreEqual(1, plugins.Count);
        Assert.AreEqual("UnitTest Plugin", plugins[0].Name);

        Assert.IsTrue(engine.SetPluginActive("test-plugin-id", false));
        var updated = engine.GetPlugin("test-plugin-id");
        Assert.IsNotNull(updated);
        Assert.IsFalse(updated.IsActive);

        Assert.IsTrue(engine.UnloadPlugin("test-plugin-id"));
        Assert.AreEqual(0, engine.GetLoadedPlugins().Count);
    }

    [TestMethod]
    public async Task NativeMcpServerShouldStartRespondAndStop()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Pixeval_McpTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "test.sqlite");

        try
        {
            using var storage = new StorageEngine(dbPath);
            using var mako = new MakoClient(new MakoConfigurationDto(
                false,
                0,
                0,
                new Dictionary<string, List<string>>(),
                null,
                "for_android",
                null,
                null
            ));
            using var download = new DownloadManager(3, null, null);
            using var cache = new CacheEngine(Path.Combine(tempDir, "cache"), 1024 * 1024);
            using var sub = new SubscriptionSyncEngine(5, null);
            using var plugin = new PluginHostEngine("5.0.0");

            var config = new McpServerConfig(
                52197,
                false,
                5,
                "5.0.13",
                "Safe"
            );

            var bridge = new MockSessionBridge();
            using var server = new McpServer(
                config,
                bridge,
                mako,
                storage,
                download,
                cache,
                sub,
                plugin
            );

            await server.StartAsync();
            Assert.IsTrue(server.IsRunning());
            Assert.AreEqual("http://127.0.0.1:52197/mcp", server.Endpoint());

            using var httpClient = new HttpClient();
            var listReq = """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""";
            using var response = await httpClient.PostAsync(
                server.Endpoint(),
                new StringContent(listReq, Encoding.UTF8, "application/json")
            );

            Assert.IsTrue(response.IsSuccessStatusCode);
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.AreEqual("2.0", root.GetProperty("jsonrpc").GetString());
            var tools = root.GetProperty("result").GetProperty("tools");
            Assert.AreEqual(38, tools.GetArrayLength());

            var initReq = """{"jsonrpc":"2.0","id":2,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test-client","version":"1.0"}}}""";
            using var initResp = await httpClient.PostAsync(
                server.Endpoint(),
                new StringContent(initReq, Encoding.UTF8, "application/json")
            );
            Assert.IsTrue(initResp.IsSuccessStatusCode);
            var initJson = await initResp.Content.ReadAsStringAsync();
            using var initDoc = JsonDocument.Parse(initJson);
            Assert.AreEqual("2024-11-05", initDoc.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString());

            await server.StopAsync();
            Assert.IsFalse(server.IsRunning());
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [TestMethod]
    public async Task NativeMcpServerWithWriteToolsShouldReturn49Tools()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Pixeval_McpTestWrite_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "test.sqlite");

        try
        {
            using var storage = new StorageEngine(dbPath);
            using var mako = new MakoClient(new MakoConfigurationDto(
                false,
                0,
                0,
                new Dictionary<string, List<string>>(),
                null,
                "for_android",
                null,
                null
            ));
            using var download = new DownloadManager(3, null, null);
            using var cache = new CacheEngine(Path.Combine(tempDir, "cache"), 1024 * 1024);
            using var sub = new SubscriptionSyncEngine(5, null);
            using var plugin = new PluginHostEngine("5.0.0");

            var config = new McpServerConfig(
                52198,
                true,
                5,
                "5.0.13",
                "Safe"
            );

            var bridge = new MockSessionBridge();
            using var server = new McpServer(
                config,
                bridge,
                mako,
                storage,
                download,
                cache,
                sub,
                plugin
            );

            await server.StartAsync();
            Assert.IsTrue(server.IsRunning());

            using var httpClient = new HttpClient();
            var listReq = """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""";
            using var response = await httpClient.PostAsync(
                server.Endpoint(),
                new StringContent(listReq, Encoding.UTF8, "application/json")
            );

            Assert.IsTrue(response.IsSuccessStatusCode);
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var tools = doc.RootElement.GetProperty("result").GetProperty("tools");
            Assert.AreEqual(49, tools.GetArrayLength());

            await server.StopAsync();
            Assert.IsFalse(server.IsRunning());
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
