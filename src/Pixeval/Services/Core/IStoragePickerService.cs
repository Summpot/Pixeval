// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace Pixeval.Services.Core;

public interface IStoragePickerService
{
    Task<IStorageFile?> SaveFileAsync(FilePickerSaveOptions options);

    Task<IReadOnlyList<IStorageFile>> OpenFilesAsync(FilePickerOpenOptions options);
}
