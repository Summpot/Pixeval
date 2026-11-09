// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.IO;
using Pixeval.Utilities;

namespace Pixeval.Models.Download.Tasks;

internal static class DownloadTaskFileHelper
{
    // Keep the previous output until the complete temporary file is committed.
    // Cancellation or a provider failure must not destroy a usable download.
    public static bool ShouldSkipExistingFile(string destination, bool overwrite) =>
        !overwrite && File.Exists(destination);

    /// <returns><see langword="true" /> if the temporary file became the destination.</returns>
    public static bool CommitDownloadedFile(string temporaryFile, string destination, bool overwrite)
    {
        if (overwrite)
        {
            FileHelper.Move(temporaryFile, destination, true);
            return true;
        }

        try
        {
            FileHelper.Move(temporaryFile, destination);
            return true;
        }
        catch (IOException) when (File.Exists(temporaryFile) && File.Exists(destination))
        {
            File.Delete(temporaryFile);
            return false;
        }
    }
}
