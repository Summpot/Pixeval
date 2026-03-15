using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Pixeval.AppManagement;

namespace Pixeval.Desktop;

internal static partial class DesktopProtocolRegistrar
{
    private const uint KcfStringEncodingUtf8 = 0x0800_0100;

    public static bool EnsureRegistered(out string? error)
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                error = "Cannot resolve current executable path for protocol registration.";
                return false;
            }

            if (OperatingSystem.IsWindows())
                EnsureRegisteredWindows(executablePath);
            else if (OperatingSystem.IsLinux())
                EnsureRegisteredLinux(executablePath);
            else if (OperatingSystem.IsMacOS())
                TryEnsureRegisteredMacOS();

            error = null;
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static void EnsureRegisteredWindows(string executablePath)
    {
        RegisterWindowsUrlProtocol("pixiv", executablePath);
        RegisterWindowsUrlProtocol("pixeval", executablePath);

        TryDeleteWindowsUserChoice("pixiv");
        TryDeleteWindowsUserChoice("pixeval");

        VerifyWindowsUrlProtocol("pixiv", executablePath);
        VerifyWindowsUrlProtocol("pixeval", executablePath);
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterWindowsUrlProtocol(string scheme, string executablePath)
    {
        var protocolPath = $"Software\\Classes\\{scheme}";

        using var protocolKey = Registry.CurrentUser.CreateSubKey(protocolPath, true)
                                ?? throw new InvalidOperationException($"Failed to open registry path: {protocolPath}");

        protocolKey.SetValue(string.Empty, $"URL:{scheme} Protocol");
        protocolKey.SetValue("URL Protocol", string.Empty);

        using (var iconKey = protocolKey.CreateSubKey("DefaultIcon", true))
            iconKey?.SetValue(string.Empty, executablePath);

        using var commandKey = protocolKey.CreateSubKey("shell\\open\\command", true)
                               ?? throw new InvalidOperationException($"Failed to open command key for protocol: {scheme}");

        commandKey.SetValue(string.Empty, $"\"{executablePath}\" \"%1\"");
    }

    [SupportedOSPlatform("windows")]
    private static void TryDeleteWindowsUserChoice(string scheme)
    {
        try
        {
            var path = $"Software\\Microsoft\\Windows\\Shell\\Associations\\UrlAssociations\\{scheme}\\UserChoice";
            Registry.CurrentUser.DeleteSubKeyTree(path, false);
        }
        catch
        {
            // ignored
        }
    }

    [SupportedOSPlatform("windows")]
    private static void VerifyWindowsUrlProtocol(string scheme, string executablePath)
    {
        var commandPath = $"Software\\Classes\\{scheme}\\shell\\open\\command";
        using var commandKey = Registry.CurrentUser.OpenSubKey(commandPath, false)
                               ?? throw new InvalidOperationException($"Protocol '{scheme}' command key is missing.");

        var command = commandKey.GetValue(string.Empty)?.ToString();
        if (string.IsNullOrWhiteSpace(command)
            || command.IndexOf(executablePath, StringComparison.OrdinalIgnoreCase) < 0)
            throw new InvalidOperationException($"Protocol '{scheme}' is not bound to current executable.");

        var userChoicePath = $"Software\\Microsoft\\Windows\\Shell\\Associations\\UrlAssociations\\{scheme}\\UserChoice";
        using var userChoiceKey = Registry.CurrentUser.OpenSubKey(userChoicePath, false);
        var progId = userChoiceKey?.GetValue("ProgId")?.ToString();

        if (!string.IsNullOrWhiteSpace(progId)
            && progId.IndexOf("pixeval", StringComparison.OrdinalIgnoreCase) < 0)
            throw new InvalidOperationException(
                $"Windows default handler for '{scheme}:' is '{progId}', not Pixeval. Please set Pixeval as default app for the {scheme}: protocol.");
    }

    [SupportedOSPlatform("linux")]
    private static void EnsureRegisteredLinux(string executablePath)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            throw new InvalidOperationException("Cannot resolve HOME path for Linux protocol registration.");

        var applicationsDir = Path.Combine(home, ".local", "share", "applications");
        Directory.CreateDirectory(applicationsDir);

        const string desktopFileName = "pixeval-url-handler.desktop";
        var desktopFilePath = Path.Combine(applicationsDir, desktopFileName);

        var escapedExec = executablePath.Replace("\"", "\\\"");
        var desktopContent = string.Join('\n',
            "[Desktop Entry]",
            "Type=Application",
            "Name=Pixeval URL Handler",
            "NoDisplay=true",
            $"Exec=\"{escapedExec}\" %u",
            "MimeType=x-scheme-handler/pixiv;x-scheme-handler/pixeval;",
            "Terminal=false",
            string.Empty);

        File.WriteAllText(desktopFilePath, desktopContent);

        RunLinuxCommand("xdg-mime", $"default {desktopFileName} x-scheme-handler/pixiv");
        RunLinuxCommand("xdg-mime", $"default {desktopFileName} x-scheme-handler/pixeval");
    }

    [SupportedOSPlatform("linux")]
    private static void RunLinuxCommand(string fileName, string args)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            process?.WaitForExit(1500);
        }
        catch
        {
            // ignore if desktop integration utilities are not available
        }
    }

    [SupportedOSPlatform("macos")]
    private static void TryEnsureRegisteredMacOS()
    {
        foreach (var scheme in new[] { "pixiv", "pixeval" })
        {
            if (TryForceMacDefaultHandler(scheme, out var warning))
                continue;

            Debug.WriteLine($"Pixeval: macOS URL scheme takeover failed for '{scheme}': {warning}");
        }
    }

    [SupportedOSPlatform("macos")]
    private static bool TryForceMacDefaultHandler(string scheme, out string? warning)
    {
        var bundleIdentifier = GetCurrentMacBundleIdentifier();
        if (string.IsNullOrWhiteSpace(bundleIdentifier))
            bundleIdentifier = AppInfo.AppIdentifier;

        var schemeRef = CreateCfString(scheme);
        if (schemeRef == IntPtr.Zero)
        {
            warning = $"Unable to allocate CFString for scheme '{scheme}'.";
            return false;
        }

        var bundleRef = CreateCfString(bundleIdentifier);
        if (bundleRef == IntPtr.Zero)
        {
            CfRelease(schemeRef);
            warning = $"Unable to allocate CFString for bundle id '{bundleIdentifier}'.";
            return false;
        }

        try
        {
            var status = LsSetDefaultHandlerForUrlScheme(schemeRef, bundleRef);
            if (status != 0)
            {
                warning = $"LSSetDefaultHandlerForURLScheme returned OSStatus {status} for bundle '{bundleIdentifier}'.";
                return false;
            }

            var copiedDefaultHandler = LsCopyDefaultHandlerForUrlScheme(schemeRef);
            if (copiedDefaultHandler == IntPtr.Zero)
            {
                warning = null;
                return true;
            }

            try
            {
                var effectiveHandler = CfStringToString(copiedDefaultHandler);
                if (!string.Equals(effectiveHandler, bundleIdentifier, StringComparison.Ordinal))
                {
                    warning = $"LaunchServices kept '{effectiveHandler}' as the default handler instead of '{bundleIdentifier}'.";
                    return false;
                }
            }
            finally
            {
                CfRelease(copiedDefaultHandler);
            }

            warning = null;
            return true;
        }
        finally
        {
            CfRelease(bundleRef);
            CfRelease(schemeRef);
        }
    }

    [SupportedOSPlatform("macos")]
    private static string? GetCurrentMacBundleIdentifier()
    {
        var mainBundle = CfBundleGetMainBundle();
        if (mainBundle == IntPtr.Zero)
            return null;

        var identifier = CfBundleGetIdentifier(mainBundle);
        return identifier == IntPtr.Zero ? null : CfStringToString(identifier);
    }

    [SupportedOSPlatform("macos")]
    private static IntPtr CreateCfString(string value)
    {
        return CfStringCreateWithCString(IntPtr.Zero, value, KcfStringEncodingUtf8);
    }

    [SupportedOSPlatform("macos")]
    private static string? CfStringToString(IntPtr value)
    {
        if (value == IntPtr.Zero)
            return null;

        var length = CfStringGetLength(value);
        if (length <= 0)
            return string.Empty;

        var maxSize = CfStringGetMaximumSizeForEncoding(length, KcfStringEncodingUtf8) + 1;
        var buffer = Marshal.AllocHGlobal(maxSize);
        try
        {
            if (!CfStringGetCString(value, buffer, maxSize, KcfStringEncodingUtf8))
                return null;

            return Marshal.PtrToStringUTF8(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", EntryPoint = "CFBundleGetMainBundle")]
    private static partial IntPtr CfBundleGetMainBundle();

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", EntryPoint = "CFBundleGetIdentifier")]
    private static partial IntPtr CfBundleGetIdentifier(IntPtr bundle);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", EntryPoint = "CFStringCreateWithCString", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr CfStringCreateWithCString(IntPtr alloc, string value, uint encoding);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", EntryPoint = "CFStringGetLength")]
    private static partial nint CfStringGetLength(IntPtr value);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", EntryPoint = "CFStringGetMaximumSizeForEncoding")]
    private static partial nint CfStringGetMaximumSizeForEncoding(nint length, uint encoding);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", EntryPoint = "CFStringGetCString")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool CfStringGetCString(IntPtr value, IntPtr buffer, nint bufferSize, uint encoding);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", EntryPoint = "CFRelease")]
    private static partial void CfRelease(IntPtr value);

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices", EntryPoint = "LSSetDefaultHandlerForURLScheme")]
    private static partial int LsSetDefaultHandlerForUrlScheme(IntPtr scheme, IntPtr handlerBundleId);

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices", EntryPoint = "LSCopyDefaultHandlerForURLScheme")]
    private static partial IntPtr LsCopyDefaultHandlerForUrlScheme(IntPtr scheme);
}
