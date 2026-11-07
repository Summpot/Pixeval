using System;
using System.Threading.Tasks;
using Avalonia.Headless;
using Avalonia.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class HeadlessTestSessionTest
{
    [TestMethod]
    public async Task AsyncAssertionFailureAfterYieldReachesTheTestRunner()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        var reachedContinuation = false;
        var exception = await Assert.ThrowsExactlyAsync<AssertFailedException>(() => session.DispatchAsync(async () =>
        {
            await Task.Yield();
            Dispatcher.UIThread.VerifyAccess();
            reachedContinuation = true;
            Assert.Fail("fixture assertion after await");
        }));

        Assert.IsTrue(reachedContinuation);
        Assert.Contains("fixture assertion after await", exception.Message);
    }
}
