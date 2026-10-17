using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Config;
using Pixeval.Services.Update;

namespace Pixeval.Tests;

[TestClass]
public sealed class NetworkSettingsResilienceTest
{
    [TestMethod]
    public void InvalidConfiguredAddressesShouldBeIgnored()
    {
        var settings = new NetworkSettingsGroup { GitHubDomainFronting = new() { GitHubNameResolver = ["not-an-ip-address", IPAddress.Loopback.ToString()] } };

        using var resolver = new Pixeval.Maho.DnsResolver();
        resolver.SetMapping("github.com", [.. settings.GitHubDomainFronting.GitHubNameResolver]);
        var addresses = resolver.GetMapping("github.com");
        Assert.HasCount(1, addresses);
        Assert.AreEqual(IPAddress.Loopback.ToString(), addresses[0]);
    }
}
