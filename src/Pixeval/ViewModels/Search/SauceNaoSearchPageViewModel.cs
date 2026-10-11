// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Config;
using Pixeval.Utilities;

namespace Pixeval.ViewModels.Search;

public sealed partial class SauceNaoSearchPageViewModel : ViewModelBase, IDisposable
{
    private bool _isDisposed;
    private CancellationTokenSource? _loadCancellation;

    public ReadOnlyMemory<byte> File { get; private set; } = ReadOnlyMemory<byte>.Empty;

    [ObservableProperty]
    public partial Bitmap? Image { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSearch))]
    public partial bool FileSelected { get; private set; }

    public static string ApiKey => App.Services!.GetRequiredService<AppSettings>().SearchSettings.SauceNaoApiKey;

    public static bool IsApiKeyExisted => !string.IsNullOrWhiteSpace(ApiKey);

    public Task LoadAsync(Stream stream) => LoadImageAsync(token => ReadImageAsync(stream, token));

    public Task LoadFileAsync(IStorageFile file) => LoadImageAsync(async token =>
    {
        await using var stream = await file.OpenReadAsync();
        return await ReadImageAsync(stream, token);
    });

    private static async Task<(Bitmap? Image, byte[] File)> ReadImageAsync(Stream stream, CancellationToken token)
    {
        using var memoryStream = Streams.RentStream();
        await stream.CopyToAsync(memoryStream, token);
        token.ThrowIfCancellationRequested();
        memoryStream.Position = 0;
        var bytes = memoryStream.ToArray();
        return (new Bitmap(memoryStream), bytes);
    }

    private async Task LoadImageAsync(Func<CancellationToken, Task<(Bitmap? Image, byte[] File)>> read)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        CancelLoad();
        using var operation = new CancellationTokenSource();
        _loadCancellation = operation;
        IsLoading = true;
        ErrorMessage = null;
        Bitmap? candidate = null;
        try
        {
            if (stream is MemoryStream ms)
            {
                File = ms.ToArray();
            }
            else
            {
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                File = memoryStream.ToArray();
            }
            FileSelected = true;
            using var readStream = new MemoryStream(File.ToArray());
            Image?.Dispose();
            Image = new Bitmap(readStream);
        }
        catch (Exception)
        {
            ErrorMessage = I18NManager.GetResource(SearchResources.SauceNAO.ImageLoadFailed);
        }
        finally
        {
            candidate?.Dispose();
            if (ReferenceEquals(_loadCancellation, operation))
            {
                _loadCancellation = null;
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    public async Task PasteAsync(Control? control)
    {
        _ = control;
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var clipboard = App.Services?.GetService<IClipboardService>() ?? NullClipboardService.Instance;
        if (await clipboard.TryGetBitmapAsync() is { } bitmap)
            try
            {
                Image?.Dispose();
                Image = bitmap;
                using var memoryStream = new MemoryStream();
                bitmap.Save(memoryStream, new PngBitmapEncoderOptions());
                return (bitmap, memoryStream.ToArray());
            }
            catch
            {
                bitmap?.Dispose();
                throw;
            }
        });
    }

    public void CancelLoad()
    {
        _loadCancellation?.Cancel();
        _loadCancellation = null;
        IsLoading = false;
    }

    public void RefreshApiKeyState()
    {
        OnPropertyChanged(nameof(IsApiKeyExisted));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        CancelLoad();
        FileSelected = false;
        File = ReadOnlyMemory<byte>.Empty;
        Image?.Dispose();
        Image = null;
    }
}
