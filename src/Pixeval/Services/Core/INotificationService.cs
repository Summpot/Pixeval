// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.Services.Core;

public interface INotificationService
{
    void ShowInformation(string title, string? content = null);

    void ShowSuccess(string title, string? content = null);

    void ShowWarning(string title, string? content = null);

    void ShowError(string title, string? content = null);
}
