using System;
using System.IO;

namespace Pixeval.Utilities;

public static class FileHelper
{
    public static bool TryCreateDirectory(string path)
    {
        try
        {
            _ = Directory.CreateDirectory(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static FileStream Open(
        string path,
        FileMode mode,
        FileAccess access,
        FileShare share,
        int bufferSize = 4096,
        FileOptions options = FileOptions.None) =>
        new(path, new FileStreamOptions
        {
            Mode = mode,
            Access = access,
            Share = share,
            BufferSize = bufferSize,
            Options = options
        });

    public static FileStream OpenRead(
        string path,
        FileShare share = FileShare.Read,
        int bufferSize = 4096,
        FileOptions options = FileOptions.None) =>
        Open(path, FileMode.Open, FileAccess.Read, share, bufferSize, options);

    public static bool TryDeleteFile(string path) => TryDeleteFile(path, out _);

    public static bool TryDeleteFile(string path, out Exception? exception)
    {
        exception = null;
        try
        {
            File.Delete(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return true;
        }
        catch (Exception e)
        {
            exception = e;
            return false;
        }
    }

    public static bool TryDeleteDirectory(string path, bool recursive = true) =>
        TryDeleteDirectory(path, recursive, out _);

    public static bool TryDeleteDirectory(string path, bool recursive, out Exception? exception)
    {
        exception = null;
        try
        {
            Directory.Delete(path, recursive);
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return true;
        }
        catch (Exception e)
        {
            exception = e;
            return false;
        }
    }

    extension(File)
    {
        public static FileStream OpenAsyncRead(string path, int bufferSize = 4096) =>
            OpenRead(path, FileShare.Read, bufferSize, FileOptions.Asynchronous);

        public static FileStream CreateAsyncWrite(string path, int bufferSize = 4096) =>
            Open(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous);
    }
}
