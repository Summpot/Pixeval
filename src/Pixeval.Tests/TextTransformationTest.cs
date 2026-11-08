using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentIcons.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Extensions.Common;
using Pixeval.Extensions.Common.Commands.Transformers;
using Pixeval.Services.Background;

namespace Pixeval.Tests;

[TestClass]
public sealed class TextTransformationTest
{
    [TestMethod]
    public async Task CancelledWaitKeepsSharedResultProtectedUntilExtensionCompletes()
    {
        var extension = new Transformer();
        using var cancellation = new CancellationTokenSource();
        var first = TextTransformation.RunAsync(extension, "first", TextTransformerType.Novel, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await first);
        var second = TextTransformation.RunAsync(extension, "second", TextTransformerType.Novel);
        Assert.HasCount(1, extension.Inputs);
        extension.Complete("late first");
        await extension.SecondCall.Task.WaitAsync(TimeSpan.FromSeconds(5));
        extension.Complete("second result");
        Assert.AreEqual("second result", await second.WaitAsync(TimeSpan.FromSeconds(5)));
        CollectionAssert.AreEqual(new[] { "late first", "second result" }, extension.ReadResults);
    }

    [TestMethod]
    public async Task CancelledQueuedCallNeverInvokesExtensionAndOtherExtensionsRemainIndependent()
    {
        var extension = new Transformer();
        var independent = new Transformer();
        var first = TextTransformation.RunAsync(extension, "first", TextTransformerType.Novel);
        using var cancellation = new CancellationTokenSource();
        var queued = TextTransformation.RunAsync(extension, "cancelled", TextTransformerType.Novel, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await queued);
        var other = TextTransformation.RunAsync(independent, "independent", TextTransformerType.Novel);
        independent.Complete("independent result");
        Assert.AreEqual("independent result", await other);
        extension.Complete("first result");
        Assert.AreEqual("first result", await first);
        Assert.HasCount(1, extension.Inputs);
    }

    [TestMethod]
    public async Task SynchronousFailureReleasesGateForRetry()
    {
        var extension = new Transformer { Throw = true };
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await TextTransformation.RunAsync(extension, "failure", TextTransformerType.Novel));
        extension.Throw = false;
        var retry = TextTransformation.RunAsync(extension, "retry", TextTransformerType.Novel);
        extension.Complete("retry result");
        Assert.AreEqual("retry result", await retry.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    private sealed class Transformer : ITextTransformerCommandExtension
    {
        public List<string> Inputs { get; } = [];
        public List<string> ReadResults { get; } = [];
        public TaskCompletionSource SecondCall { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Throw { get; set; }
        private ITaskCompletionSource? _pending;
        private string _result = "";
        public void TransformAsync(ITaskCompletionSource task, string originalString, TextTransformerType type)
        {
            if (Throw) throw new InvalidOperationException("Fixed failure");
            Assert.IsNull(_pending, "Calls to an extension's shared result slot must not overlap.");
            Inputs.Add(originalString);
            _pending = task;
            if (Inputs.Count == 2) SecondCall.SetResult();
        }
        public void Complete(string result)
        {
            var task = _pending!;
            _result = result;
            _pending = null;
            task.SetCompleted();
        }
        public string GetTransformResult() { ReadResults.Add(_result); return _result; }
        public Symbol GetIcon() => Symbol.Translate;
        public string GetLabel() => "Fixture";
        public string GetDescription() => "Fixture";
        public void OnExtensionLoaded() { }
        public void OnExtensionUnloaded() { }
    }
}
