using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Pixeval.Tests.Ui;

[TestClass]
public sealed class UiTestAssembly
{
    [AssemblyCleanup]
    public static async Task CleanupAsync() => await UiTestSession.DisposeAsync();
}
