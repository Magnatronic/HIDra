namespace HIDra.Core.Configuration;

/// <summary>
/// Writing and reading the files that hold someone's settings and practice, so that a
/// save cut off part way - at logoff, when the network drops, or when the PC is switched
/// off - can never leave them with an empty or half-written file.
///
/// A save is written in full to a temporary file first, then swapped in, and the file it
/// replaces is kept as a backup. Reading falls back to that backup if the main file is
/// missing or cannot be read.
/// </summary>
public static class SafeFile
{
    private const string TempSuffix = ".saving";
    private const string BackupSuffix = ".bak";

    /// <summary>
    /// Write text to a file, replacing it only once the new text is safely on disk.
    /// Throws if it cannot be written, as File.WriteAllText does.
    /// </summary>
    public static void WriteAllText(string path, string text)
    {
        string temp = path + TempSuffix;
        File.WriteAllText(temp, text);

        if (File.Exists(path))
        {
            // Swaps the new file in and keeps the old one as the backup, in one step
            File.Replace(temp, path, path + BackupSuffix, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temp, path);
        }
    }

    /// <summary>
    /// The file's text if it can be read and makes sense, otherwise its backup's, otherwise
    /// null. "Makes sense" is for the caller to say: a file that exists but was cut off
    /// part way reads fine and only fails when it is understood.
    /// </summary>
    public static T? Read<T>(string path, Func<string, T?> understand) where T : class
    {
        foreach (string candidate in new[] { path, path + BackupSuffix })
        {
            try
            {
                if (File.Exists(candidate) && understand(File.ReadAllText(candidate)) is T value)
                {
                    return value;
                }
            }
            catch
            {
                // Unreadable or not understood: try the backup
            }
        }

        return null;
    }
}
