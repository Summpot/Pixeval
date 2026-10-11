// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace Pixeval.Services.Core;

public sealed class StoragePickerService : IStoragePickerService
{
    private readonly IUiHost _host;

    public StoragePickerService(IUiHost host) => _host = host;

    public Task<IStorageFile?> SaveFileAsync(FilePickerSaveOptions options) =>
        _host.TopLevel?.StorageProvider?.SaveFilePickerAsync(options) ?? Task.FromResult<IStorageFile?>(null);

    public async Task<IReadOnlyList<IStorageFile>> OpenFilesAsync(FilePickerOpenOptions options)
    {
        if (_host.TopLevel?.StorageProvider is not { } provider)
            return [];

        return await provider.OpenFilePickerAsync(options);
    }
}

public sealed class NullStoragePickerService : IStoragePickerService
{
    public static NullStoragePickerService Instance { get; } = new();

    public Task<IStorageFile?> SaveFileAsync(FilePickerSaveOptions options) =>
        Task.FromResult<IStorageFile?>(null);

    public Task<IReadOnlyList<IStorageFile>> OpenFilesAsync(FilePickerOpenOptions options) =>
        Task.FromResult<IReadOnlyList<IStorageFile>>([]);
}
