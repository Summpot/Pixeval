using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;

namespace Pixeval.Desktop;

internal static class AngleNativeLoader
{
    public static void EnsureLoaded()
    {
        var assembly = typeof(AngleNativeLoader).Assembly;
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.Contains("av_libglesv2", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (resourceNames.Length == 0)
        {
            return;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var version = assembly.GetName().Version?.ToString() ?? "default";
        var cacheDir = Path.Combine(appData, "Pixeval", "natives", "angle", version);

        foreach (var resourceName in resourceNames)
        {
            var fileName = resourceName;
            if (resourceName.Contains('/') || resourceName.Contains('\\'))
            {
                fileName = Path.GetFileName(resourceName);
            }

            var targetPath = Path.Combine(cacheDir, fileName);

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    continue;
                }

                if (!File.Exists(targetPath) || new FileInfo(targetPath).Length != stream.Length)
                {
                    Directory.CreateDirectory(cacheDir);
                    var tmpPath = Path.Combine(cacheDir, $"{fileName}.{Guid.NewGuid():N}.tmp");
                    try
                    {
                        using (var fs = File.Create(tmpPath))
                        {
                            stream.CopyTo(fs);
                        }

                        if (!OperatingSystem.IsWindows())
                        {
                            File.SetUnixFileMode(tmpPath,
                                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                        }

                        File.Move(tmpPath, targetPath, overwrite: true);
                    }
                    catch (IOException) when (File.Exists(targetPath))
                    {
                        try
                        {
                            File.Delete(tmpPath);
                        }
                        catch
                        {
                            // ignore cleanup error
                        }
                    }
                }
            }

            if (!File.Exists(targetPath))
            {
                continue;
            }

            try
            {
                NativeLibrary.Load(targetPath);
            }
            catch
            {
                // Let subsequent Avalonia P/Invoke report detailed load errors if any
            }

            IntPtr Resolver(string libraryName, Assembly asm, DllImportSearchPath? searchPath)
            {
                if (libraryName.Contains("av_libglesv2", StringComparison.OrdinalIgnoreCase))
                {
                    return NativeLibrary.Load(targetPath);
                }
                return IntPtr.Zero;
            }

            try
            {
                NativeLibrary.SetDllImportResolver(typeof(AppBuilder).Assembly, Resolver);
            }
            catch (InvalidOperationException)
            {
                // Resolver already registered
            }

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    NativeLibrary.SetDllImportResolver(typeof(Win32PlatformOptions).Assembly, Resolver);
                }
                catch (InvalidOperationException)
                {
                    // Resolver already registered
                }
            }
        }
    }
}
