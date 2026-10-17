using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Ffi;

namespace Pixeval.Tests;

[TestClass]
public sealed class NativeSmokeTest
{
    [TestMethod]
    public void RustCoreShouldExposeBothBindingContracts()
    {
        var library = NativeLibrary.Load("pixeval_native", typeof(PixevalNativeMethods).Assembly, null);
        try
        {
            Assert.IsTrue(NativeLibrary.TryGetExport(library, "uniffi_pixeval_native_fn_func_version", out _));
            Assert.IsTrue(NativeLibrary.TryGetExport(library, "boltffi_function_pixeval_native_version", out _));
            Assert.IsTrue(NativeLibrary.TryGetExport(library, "uniffi_pixeval_maho_fn_func_is_pixiv_host", out _));
            Assert.IsTrue(NativeLibrary.TryGetExport(library, "boltffi_function_pixeval_maho_is_pixiv_host", out _));
            Assert.IsTrue(NativeLibrary.TryGetExport(library, "boltffi_function_pixeval_native_ping", out _));
        }
        finally
        {
            NativeLibrary.Free(library);
        }
    }

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
