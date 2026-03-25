using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Xml.Linq;
using Microsoft.Win32;

namespace Pixeval.Desktop;

internal static partial class DesktopProtocolRegistrar
{
    private const uint KcfStringEncodingUtf8 = 0x0800_0100;

    private const string MacBundleName = "Pixeval";

    private const string MacBundleIdentifier = "io.github.summpot.pixeval";

    private const string MacInfoPlistFileName = "Info.plist";

    private static readonly string[] MacSchemes = ["pixiv", "pixeval"];

    private const string FallbackMacInfoPlistTemplate = """
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>CFBundleDevelopmentRegion</key>
	<string>en</string>
	<key>CFBundleDisplayName</key>
	<string>Pixeval</string>
	<key>CFBundleExecutable</key>
	<string>__EXECUTABLE_NAME__</string>
	<key>CFBundleIdentifier</key>
	<string>io.github.summpot.pixeval</string>
	<key>CFBundleInfoDictionaryVersion</key>
	<string>6.0</string>
	<key>CFBundleName</key>
	<string>Pixeval</string>
	<key>CFBundlePackageType</key>
	<string>APPL</string>
	<key>CFBundleShortVersionString</key>
	<string>1.0</string>
	<key>CFBundleVersion</key>
	<string>1</string>
	<key>LSMinimumSystemVersion</key>
	<string>11.0</string>
	<key>NSHighResolutionCapable</key>
	<true/>
	<key>CFBundleURLTypes</key>
	<array>
		<dict>
			<key>CFBundleTypeRole</key>
			<string>Viewer</string>
			<key>CFBundleURLName</key>
			<string>io.github.summpot.pixeval.oauth</string>
			<key>CFBundleURLSchemes</key>
			<array>
				<string>pixiv</string>
				<string>pixeval</string>
			</array>
		</dict>
	</array>
</dict>
</plist>
""";

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
                EnsureRegisteredMacOS(executablePath);

            error = null;
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    public static bool TryRelaunchAsMacBundle(string[] args)
    {
        if (!OperatingSystem.IsMacOS())
            return false;

        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                return false;

            var bundle = EnsureMacAppBundle(executablePath);
            RegisterMacBundle(bundle);

            if (string.Equals(
                    Path.GetFullPath(executablePath),
                    Path.GetFullPath(bundle.ExecutablePath),
                    StringComparison.OrdinalIgnoreCase))
                return false;

            LaunchMacBundle(bundle.BundlePath, args);
            return true;
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Pixeval: macOS bundle relaunch failed: {e.Message}");
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
            iconKey.SetValue(string.Empty, executablePath);

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
    private static void EnsureRegisteredMacOS(string executablePath)
    {
        var bundle = EnsureMacAppBundle(executablePath);
        RegisterMacBundle(bundle);
    }

    [SupportedOSPlatform("macos")]
    private static void RegisterMacBundle(MacAppBundle bundle)
    {
        RegisterBundleWithLaunchServices(bundle.BundlePath);

        foreach (var scheme in MacSchemes)
            SetMacDefaultHandler(scheme, bundle.BundleIdentifier);
    }

    [SupportedOSPlatform("macos")]
    private static MacAppBundle EnsureMacAppBundle(string executablePath)
    {
        if (TryGetCurrentMacAppBundle(executablePath, out var currentBundle)
            && HasRequiredMacSchemes(currentBundle))
            return currentBundle;

        var executableName = Path.GetFileName(executablePath);
        if (string.IsNullOrWhiteSpace(executableName))
            throw new InvalidOperationException("Cannot determine macOS executable name for bundle registration.");

        var sourceDirectory = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(sourceDirectory))
            throw new InvalidOperationException("Cannot determine macOS output directory for bundle registration.");

        var applicationsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Applications");
        Directory.CreateDirectory(applicationsDirectory);

        var bundlePath = Path.Combine(applicationsDirectory, $"{MacBundleName}.app");
        var contentsPath = Path.Combine(bundlePath, "Contents");
        var macOsPath = Path.Combine(contentsPath, "MacOS");
        Directory.CreateDirectory(macOsPath);

        SyncDirectory(sourceDirectory, macOsPath);

        var infoPlistPath = Path.Combine(contentsPath, MacInfoPlistFileName);
        var infoPlistContent = LoadMacInfoPlistTemplate(sourceDirectory)
            .Replace("__EXECUTABLE_NAME__", executableName, StringComparison.Ordinal)
            .Replace("io.github.summpot.pixeval", MacBundleIdentifier, StringComparison.Ordinal);

        File.WriteAllText(infoPlistPath, infoPlistContent);
        File.WriteAllText(Path.Combine(contentsPath, "PkgInfo"), "APPL????");

        var bundledExecutablePath = Path.Combine(macOsPath, executableName);
        if (!File.Exists(bundledExecutablePath))
            throw new InvalidOperationException($"Bundled executable was not created at '{bundledExecutablePath}'.");

        return new MacAppBundle(bundlePath, bundledExecutablePath, MacBundleIdentifier, MacSchemes);
    }

    [SupportedOSPlatform("macos")]
    private static string LoadMacInfoPlistTemplate(string sourceDirectory)
    {
        var templatePath = Path.Combine(sourceDirectory, MacInfoPlistFileName);
        return File.Exists(templatePath)
            ? File.ReadAllText(templatePath)
            : FallbackMacInfoPlistTemplate;
    }

    [SupportedOSPlatform("macos")]
    private static bool TryGetCurrentMacAppBundle(string executablePath, out MacAppBundle bundle)
    {
        bundle = null!;

        var executableDirectory = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(executableDirectory))
            return false;

        var macOsDirectory = new DirectoryInfo(executableDirectory);
        if (!string.Equals(macOsDirectory.Name, "MacOS", StringComparison.Ordinal))
            return false;

        var contentsDirectory = macOsDirectory.Parent;
        if (contentsDirectory is null || !string.Equals(contentsDirectory.Name, "Contents", StringComparison.Ordinal))
            return false;

        var bundleDirectory = contentsDirectory.Parent;
        if (bundleDirectory is null || !string.Equals(bundleDirectory.Extension, ".app", StringComparison.OrdinalIgnoreCase))
            return false;

        var infoPlistPath = Path.Combine(contentsDirectory.FullName, MacInfoPlistFileName);
        if (!File.Exists(infoPlistPath))
            return false;

        var metadata = ReadMacBundleMetadata(infoPlistPath);
        bundle = new MacAppBundle(bundleDirectory.FullName, executablePath, metadata.BundleIdentifier, metadata.UrlSchemes);
        return true;
    }

    [SupportedOSPlatform("macos")]
    private static MacBundleMetadata ReadMacBundleMetadata(string infoPlistPath)
    {
        var document = XDocument.Load(infoPlistPath);
        var plist = document.Root ?? throw new InvalidOperationException($"Info.plist '{infoPlistPath}' has no root element.");
        var dict = plist.Element("dict") ?? throw new InvalidOperationException($"Info.plist '{infoPlistPath}' has no top-level dict.");
        var elements = dict.Elements().ToArray();

        string? bundleIdentifier = null;
        string[] urlSchemes = [];

        for (var index = 0; index < elements.Length - 1; index++)
        {
            if (!string.Equals(elements[index].Name.LocalName, "key", StringComparison.Ordinal))
                continue;

            var key = elements[index].Value;
            var value = elements[index + 1];

            if (string.Equals(key, "CFBundleIdentifier", StringComparison.Ordinal))
                bundleIdentifier = value.Value.Trim();

            if (string.Equals(key, "CFBundleURLTypes", StringComparison.Ordinal)
                && string.Equals(value.Name.LocalName, "array", StringComparison.Ordinal))
            {
                var entries = value.Elements("dict").ToArray();
                urlSchemes = entries
                    .SelectMany(entry =>
                    {
                        var entryElements = entry.Elements().ToArray();
                        for (var entryIndex = 0; entryIndex < entryElements.Length - 1; entryIndex++)
                        {
                            if (!string.Equals(entryElements[entryIndex].Name.LocalName, "key", StringComparison.Ordinal)
                                || !string.Equals(entryElements[entryIndex].Value, "CFBundleURLSchemes", StringComparison.Ordinal)
                                || !string.Equals(entryElements[entryIndex + 1].Name.LocalName, "array", StringComparison.Ordinal))
                                continue;

                            return entryElements[entryIndex + 1].Elements("string").Select(element => element.Value.Trim());
                        }

                        return Enumerable.Empty<string>();
                    })
                    .Where(valueText => !string.IsNullOrWhiteSpace(valueText))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        }

        if (string.IsNullOrWhiteSpace(bundleIdentifier))
            throw new InvalidOperationException($"Info.plist '{infoPlistPath}' is missing CFBundleIdentifier.");

        return new MacBundleMetadata(bundleIdentifier, urlSchemes);
    }

    [SupportedOSPlatform("macos")]
    private static bool HasRequiredMacSchemes(MacAppBundle bundle)
    {
        return MacSchemes.All(scheme => bundle.UrlSchemes.Any(existing => string.Equals(existing, scheme, StringComparison.OrdinalIgnoreCase)));
    }

    [SupportedOSPlatform("macos")]
    private static void SyncDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.GetDirectories(sourceDirectory))
        {
            var directoryName = Path.GetFileName(directory);
            if (directoryName.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                continue;

            SyncDirectory(directory, Path.Combine(destinationDirectory, directoryName));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory))
            CopyFile(file, Path.Combine(destinationDirectory, Path.GetFileName(file)));
    }

    [SupportedOSPlatform("macos")]
    private static void CopyFile(string sourcePath, string destinationPath)
    {
        var sourceInfo = new FileInfo(sourcePath);
        var destinationInfo = new FileInfo(destinationPath);
        if (destinationInfo.Exists
            && destinationInfo.Length == sourceInfo.Length
            && destinationInfo.LastWriteTimeUtc == sourceInfo.LastWriteTimeUtc)
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(sourcePath, destinationPath, true);
        File.SetLastWriteTimeUtc(destinationPath, sourceInfo.LastWriteTimeUtc);
        TryCopyUnixFileMode(sourcePath, destinationPath);
    }

    [SupportedOSPlatform("macos")]
    private static void TryCopyUnixFileMode(string sourcePath, string destinationPath)
    {
        try
        {
            File.SetUnixFileMode(destinationPath, File.GetUnixFileMode(sourcePath));
        }
        catch
        {
            // ignored
        }
    }

    [SupportedOSPlatform("macos")]
    private static void LaunchMacBundle(string bundlePath, string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/open",
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("-a");
        startInfo.ArgumentList.Add(bundlePath);

        if (args.Length > 0)
        {
            startInfo.ArgumentList.Add("--args");
            foreach (var arg in args)
                startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
            throw new InvalidOperationException($"Failed to relaunch macOS bundle at '{bundlePath}'.");
    }

    [SupportedOSPlatform("macos")]
    private static void RegisterBundleWithLaunchServices(string bundlePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add(bundlePath);

        using var process = Process.Start(startInfo);
        if (process is null)
            throw new InvalidOperationException("Failed to start lsregister for macOS protocol registration.");

        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"lsregister failed for '{bundlePath}': {process.StandardError.ReadToEnd()}");
    }

    [SupportedOSPlatform("macos")]
    private static void SetMacDefaultHandler(string scheme, string bundleIdentifier)
    {
        var schemeRef = CreateCfString(scheme);
        if (schemeRef == IntPtr.Zero)
            throw new InvalidOperationException($"Unable to allocate CFString for scheme '{scheme}'.");

        var bundleRef = CreateCfString(bundleIdentifier);
        if (bundleRef == IntPtr.Zero)
        {
            CfRelease(schemeRef);
            throw new InvalidOperationException($"Unable to allocate CFString for bundle id '{bundleIdentifier}'.");
        }

        try
        {
            var status = LsSetDefaultHandlerForUrlScheme(schemeRef, bundleRef);
            if (status != 0)
                throw new InvalidOperationException($"LSSetDefaultHandlerForURLScheme returned OSStatus {status} for bundle '{bundleIdentifier}'.");

            var copiedDefaultHandler = LsCopyDefaultHandlerForUrlScheme(schemeRef);
            if (copiedDefaultHandler == IntPtr.Zero)
                return;

            try
            {
                var effectiveHandler = CfStringToString(copiedDefaultHandler);
                if (!string.Equals(effectiveHandler, bundleIdentifier, StringComparison.Ordinal))
                    throw new InvalidOperationException($"LaunchServices kept '{effectiveHandler}' as the default handler instead of '{bundleIdentifier}'.");
            }
            finally
            {
                CfRelease(copiedDefaultHandler);
            }
        }
        finally
        {
            CfRelease(bundleRef);
            CfRelease(schemeRef);
        }
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

    private sealed record MacAppBundle(string BundlePath, string ExecutablePath, string BundleIdentifier, string[] UrlSchemes);

    private sealed record MacBundleMetadata(string BundleIdentifier, string[] UrlSchemes);
}
