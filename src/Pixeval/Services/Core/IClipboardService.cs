// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace Pixeval.Services.Core;

public interface IClipboardService
{
    Task SetTextAsync(string? text);

    Task<string?> GetTextAsync();

    Task SetBitmapAsync(Bitmap bitmap);

    Task<Bitmap?> TryGetBitmapAsync();

    Task FlushAsync();
}
