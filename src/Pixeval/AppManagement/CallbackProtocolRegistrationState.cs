using System;

namespace Pixeval.AppManagement;

public static class CallbackProtocolRegistrationState
{
    public static Func<(bool IsReady, string? Error)>? EnsureRegistration { get; set; }

    public static bool IsReady { get; set; } = true;

    public static string? LastError { get; set; }

    public static bool EnsureReadyNow()
    {
        if (EnsureRegistration is null)
            return IsReady;

        var (isReady, error) = EnsureRegistration();
        IsReady = isReady;
        LastError = error;
        return isReady;
    }
}
