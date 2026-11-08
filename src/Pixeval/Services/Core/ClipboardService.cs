// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Threading.Tasks;
using Pixeval.Utilities;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;

namespace Pixeval.Services.Core;

public sealed class ClipboardService : IClipboardService
{
    private readonly IUiHost _host;

    public ClipboardService(IUiHost host) => _host = host;

    public Task SetTextAsync(string? text) =>
        _host.TopLevel?.Clipboard?.SetTextAsync(text) ?? Task.CompletedTask;

    public Task<string?> GetTextAsync() =>
        _host.TopLevel?.Clipboard?.TryGetTextAsync() ?? Task.FromResult<string?>(null);

    public async Task SetBitmapAsync(Bitmap bitmap)
    {
        if (_host.TopLevel?.Clipboard is not { } clipboard)
            return;
        var transfer = new BitmapDataTransfer(bitmap);
        try
        {
            await clipboard.SetDataAsync(transfer);
        }
        catch
        {
            transfer.Dispose();
            throw;
        }
    }

    public Task<Bitmap?> TryGetBitmapAsync() =>
        _host.TopLevel?.Clipboard?.TryGetBitmapAsync() ?? Task.FromResult<Bitmap?>(null);

    public Task FlushAsync() =>
        _host.TopLevel?.Clipboard?.FlushAsync() ?? Task.CompletedTask;
}

public sealed class NullClipboardService : IClipboardService
{
    public static NullClipboardService Instance { get; } = new();

    public Task SetTextAsync(string? text) => Task.CompletedTask;

    public Task<string?> GetTextAsync() => Task.FromResult<string?>(null);

    public Task SetBitmapAsync(Bitmap bitmap) => Task.CompletedTask;

    public Task<Bitmap?> TryGetBitmapAsync() => Task.FromResult<Bitmap?>(null);

    public Task FlushAsync() => Task.CompletedTask;
}
