// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Threading.Tasks;
using Pixeval.Views.Shell;

namespace Pixeval.Services.Update;

public interface IAppUpdateNotificationCoordinator
{
    Task CheckAndNotifyUpdatesAsync(ViewContainerBase viewContainer);
}
