// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;

namespace Pixeval.Common;

internal static class LoginErrorHelper
{
    public static string GetMessage(Exception exception) => IsStorageFailure(exception)
        ? Strings.LoginPageStorageFailureFormatted(
            AppInfo.ApplicationFolderPath, GetDetails(exception.GetBaseException()))
        : exception.GetBaseException().Message;

    private static string GetDetails(Exception exception) => exception switch
    {
        StorageError.Database database => database.Message,
        StorageError.ConstraintViolation constraint => constraint.Message,
        _ => exception.Message
    };

    internal static bool IsStorageFailure(Exception exception)
    {
        var hasIoFailure = false;
        var hasNetworkFailure = false;
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is StorageError or UnauthorizedAccessException)
                return true;
            hasIoFailure |= current is IOException;
            hasNetworkFailure |= current is HttpRequestException or HttpIOException or SocketException or AuthenticationException;
        }

        return hasIoFailure && !hasNetworkFailure;
    }
}
