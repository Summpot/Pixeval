using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Native;

namespace Pixeval.Tests;

[TestClass]
public sealed class NativeSmokeTest
{
    [TestMethod]
    public void RustCoreVersionShouldMatch()
    {
        var version = PixevalNativeMethods.Version();
        Assert.IsNotNull(version);
        Assert.AreEqual("0.1.0", version);
    }

    [TestMethod]
    public async Task RustCorePingShouldRespond()
    {
        var response = await PixevalNativeMethods.PingAsync("hello rust");
        Assert.AreEqual("pong: hello rust", response);
    }
}
