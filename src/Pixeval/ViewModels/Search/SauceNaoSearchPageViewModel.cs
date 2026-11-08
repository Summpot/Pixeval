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

    private readonly AppSettings _appSettings;
    private readonly IClipboardService _clipboard;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSearch))]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    public bool CanSearch => FileSelected && !IsLoading;

    public SauceNaoSearchPageViewModel(AppSettings appSettings, IClipboardService clipboard)
    {
        _appSettings = appSettings;
        _clipboard = clipboard;
    }

    public string ApiKey => _appSettings.SearchSettings.SauceNaoApiKey;

    public bool IsApiKeyExisted => !string.IsNullOrWhiteSpace(ApiKey);

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
            (candidate, var bytes) = await read(operation.Token);
            if (operation.IsCancellationRequested || _isDisposed)
                return;
            if (candidate is null)
            {
                ErrorMessage = Strings.SearchSauceNAOClipboardHasNoImage();
                return;
            }

            // Publish only a completely decoded image/file pair. A failed replacement keeps
            // the previous selection available, and a late read never owns the preview.
            var previous = Image;
            File = bytes;
            Image = candidate;
            candidate = null;
            FileSelected = true;
            previous?.Dispose();
        }
        catch (Exception) when (operation.IsCancellationRequested || _isDisposed)
        {
            // A closed page or a newer image owns the state, including any error message.
        }
        catch (Exception)
        {
            ErrorMessage = Strings.SearchSauceNAOImageLoadFailed();
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

        await LoadImageAsync(async token =>
        {
            var bitmap = await _clipboard.TryGetBitmapAsync();
            try
            {
                token.ThrowIfCancellationRequested();
                if (bitmap is null)
                    return (null, []);
                using var memoryStream = Streams.RentStream();
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
