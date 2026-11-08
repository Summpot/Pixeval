// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Pixeval.Extensions.Common.Commands.Transformers;

namespace Pixeval.Services.Background;

internal static class TextTransformation
{
    private static readonly ConditionalWeakTable<ITextTransformerCommandExtension, SemaphoreSlim> _Gates = new();

    public static Task<string> RunAsync(ITextTransformerCommandExtension extension, string text,
        TextTransformerType type, CancellationToken cancellationToken = default) =>
        RunCoreAsync(extension, text, type, cancellationToken).WaitAsync(cancellationToken);

    private static async Task<string> RunCoreAsync(ITextTransformerCommandExtension extension, string text,
        TextTransformerType type, CancellationToken cancellationToken)
    {
        var gate = _Gates.GetValue(extension, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // The SDK exposes a shared GetTransformResult slot and no cancellation ABI.
            // Keep this gate until that result is read, even if the caller stops waiting.
            return await extension.TransformAsync(text, type);
        }
        finally
        {
            gate.Release();
        }
    }
}
