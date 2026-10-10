// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading.Tasks;
using Pixeval.Controls;

namespace Pixeval.Services.Core;

public interface IDialogService
{
    Task<ContentDialogResult> CreateOkCancelAsync(string title, object? content);

    Task<ContentDialogResult> CreateAcknowledgementAsync(string title, object? content);

    Task<ContentDialogResult> CreateAcknowledgementTaskAsync(string title, Func<ContentDialog, Task> updateAsync);
}
