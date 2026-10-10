using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Models.Navigation;
using Pixeval.Services;
using Pixeval.Utilities;

namespace Pixeval.Tests;

[TestClass]
public sealed class NavigationServiceTest
{
    [TestMethod]
    public void ResolveViewContainer_ReturnsNull_WhenNoVisualContextProvided()
    {
        var service = new NavigationService();
        var container = service.ResolveViewContainer(null);
        Assert.IsNull(container);
    }

    [TestMethod]
    public void RegistryLookup_ResolvesRegisteredPages()
    {
        // Force registration
        _ = AvaloniaHelper.PageTypeToHeaderMap;

        Assert.IsTrue(NavigationPageRegistry.TryGetPage("Search", out var searchDef));
        Assert.IsNotNull(searchDef);
        Assert.AreEqual("Search", searchDef.Key);

        Assert.IsFalse(NavigationPageRegistry.TryGetPage("InvalidUnknownPageKey", out var unknownDef));
        Assert.IsNull(unknownDef);
    }

    [TestMethod]
    public void NavigateToKey_WithInvalidKey_DoesNotThrow()
    {
        var service = new NavigationService();
        service.NavigateToKey("InvalidNonExistentPageKey", null, false, null);
    }
}
