using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless;

namespace Pixeval.Tests;

internal static class HeadlessTestSessionExtensions
{
    extension(HeadlessUnitTestSession session)
    {
        internal Task DispatchAsync(Func<Task> action, CancellationToken token = default) =>
            // Dispatch(Func<Task>) binds to Func<TResult> with TResult=Task. Its outer
            // task completes before assertions after an await. Force the async overload
            // so the headless dispatcher remains alive until the entire test completes.
            session.Dispatch<bool>(async () =>
            {
                await action();
                return true;
            }, token);
    }
}
