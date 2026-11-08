using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Views.ViewContainers;

namespace Pixeval.Tests.Ui;

[TestClass]
public sealed class UiArchitectureTests
{
    [TestMethod]
    public void TestAssemblyDoesNotDefineAnAlternativeNavigationContainer()
    {
        var alternatives = typeof(UiArchitectureTests).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(ViewContainerBase)))
            .Select(type => type.FullName).ToArray();
        Assert.IsEmpty(alternatives, "UI tests must compose the production shell, not introduce a test navigation/lifetime implementation.");
    }
}
