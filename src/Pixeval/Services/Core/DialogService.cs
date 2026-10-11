// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading.Tasks;
using Pixeval.Controls;

namespace Pixeval.Services.Core;

public sealed class DialogService : IDialogService
{
    private readonly IUiHost _host;

    public DialogService(IUiHost host) => _host = host;

    public Task<ContentDialogResult> CreateOkCancelAsync(string title, object? content) =>
        _host.Container?.CreateOkCancelAsync(title, content) ?? Task.FromResult(ContentDialogResult.None);

    public Task<ContentDialogResult> CreateAcknowledgementAsync(string title, object? content) =>
        _host.Container?.CreateAcknowledgementAsync(title, content) ?? Task.FromResult(ContentDialogResult.None);

    public Task<ContentDialogResult> CreateAcknowledgementTaskAsync(string title, Func<ContentDialog, Task> updateAsync) =>
        _host.Container?.CreateAcknowledgementTaskAsync(title, updateAsync) ?? Task.FromResult(ContentDialogResult.None);
}

public sealed class NullDialogService : IDialogService
{
    public static NullDialogService Instance { get; } = new();

    public Task<ContentDialogResult> CreateOkCancelAsync(string title, object? content) =>
        Task.FromResult(ContentDialogResult.None);

    public Task<ContentDialogResult> CreateAcknowledgementAsync(string title, object? content) =>
        Task.FromResult(ContentDialogResult.None);

    public Task<ContentDialogResult> CreateAcknowledgementTaskAsync(string title, Func<ContentDialog, Task> updateAsync) =>
        Task.FromResult(ContentDialogResult.None);
}
