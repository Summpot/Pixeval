using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.I18N;
using Pixeval.Utilities;
using Pixeval.ViewModels.Search;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SauceNaoImageInputTest
{
    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        I18NManager.CandidatePaths.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Pixeval")));
        I18NManager.Register(new JsonMarkdownLangPlugin(), LanguageHelper.DefaultLanguage);
    }

    [TestMethod]
    public async Task InvalidReplacementKeepsPreviewAndAllowsRetry()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerRenderingTest.ViewerTestApplication));
        _ = await session.Dispatch<bool>(async () =>
        {
            using var model = new SauceNaoSearchPageViewModel();
            var bytes = Sample();
            await model.LoadAsync(new MemoryStream(bytes));
            var preview = model.Image;
            await model.LoadAsync(new MemoryStream([1, 2, 3]));
            Assert.IsTrue(model.FileSelected, "An invalid replacement must retain the last valid selection.");
            Assert.AreSame(preview, model.Image);
            CollectionAssert.AreEqual(bytes, model.File.ToArray());
            Assert.IsFalse(string.IsNullOrWhiteSpace(model.ErrorMessage));
            await model.LoadAsync(new MemoryStream(bytes));
            Assert.IsTrue(model.FileSelected, "Decode failure must not permanently dispose the input model.");
            Assert.AreNotSame(preview, model.Image);
            Assert.IsNull(model.ErrorMessage);
            return true;
        }, CancellationToken.None);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LateReadCannotReplaceNewSelectionOrReviveDisposedModel(bool dispose)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerRenderingTest.ViewerTestApplication));
        _ = await session.Dispatch<bool>(async () =>
        {
            using var model = new SauceNaoSearchPageViewModel();
            using var delayed = new DelayedStream(Sample());
            var loading = model.LoadAsync(delayed);
            await delayed.Started.Task;
            Assert.IsTrue(model.IsLoading);
            Assert.IsFalse(model.CanSearch);
            if (dispose)
                model.Dispose();
            else
                await model.LoadAsync(new MemoryStream(Sample()));
            var current = model.Image;
            delayed.Release.SetResult();
            await loading;
            Assert.AreSame(current, model.Image, "A late read must not publish an obsolete bitmap.");
            Assert.AreEqual(!dispose, model.FileSelected);
            Assert.AreEqual(dispose, model.File.IsEmpty);
            Assert.IsFalse(model.IsLoading);
            Assert.IsTrue(delayed.Token.IsCancellationRequested);
            return true;
        }, CancellationToken.None);
    }

    [TestMethod]
    public async Task CancelReadKeepsSelectionAndIgnoresLateError()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerRenderingTest.ViewerTestApplication));
        _ = await session.Dispatch<bool>(async () =>
        {
            using var model = new SauceNaoSearchPageViewModel();
            using var valid = new MemoryStream(Sample());
            await model.LoadAsync(valid);
            var image = model.Image;
            using var delayed = new DelayedStream(Sample());
            var loading = model.LoadAsync(delayed);
            await delayed.Started.Task;
            Assert.IsFalse(model.CanSearch, "The previous image must not be submitted while a replacement is loading.");
            model.CancelLoad();
            Assert.IsTrue(model.CanSearch);
            delayed.Release.SetException(new IOException("Fixed late read failure"));
            await loading;
            Assert.AreSame(image, model.Image);
            Assert.IsNull(model.ErrorMessage);
            Assert.IsFalse(model.IsLoading);
            Assert.IsTrue(valid.CanRead, "LoadAsync must not close a caller-owned stream.");
            valid.Position = 0;
            await model.LoadAsync(valid);
            Assert.IsTrue(model.CanSearch, "Canceling a page attachment must allow a later load.");
            return true;
        }, CancellationToken.None);
    }

    private static byte[] Sample() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ProgressiveImages", "progressive.jpg"));

    private sealed class DelayedStream(byte[] bytes) : MemoryStream(bytes)
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationToken Token { get; private set; }

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            Started.SetResult();
            await Release.Task; // Deliberately ignore cancellation to exercise ownership checks.
            await base.CopyToAsync(destination, bufferSize, CancellationToken.None);
        }
    }
}
