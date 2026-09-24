using System.Reflection;

namespace HIDra.Core.Configuration;

/// <summary>
/// A record of errors HIDra did not expect, in HIDra-errors.log beside the person's
/// settings, so that when something goes wrong on a PC there is something to look at.
/// Writing it must never cause a problem of its own, so every failure here is ignored.
/// </summary>
public static class ErrorLog
{
    private const string FileName = "HIDra-errors.log";

    /// <summary>Past this size the log starts again, keeping the last one as .old</summary>
    private const long MaxBytes = 256 * 1024;

    private static readonly object Gate = new();

    /// <summary>Where the log is, or would be once something has gone wrong</summary>
    public static string Location
    {
        get
        {
            try
            {
                return Path.Combine(UserSettingsStore.Folder, FileName);
            }
            catch
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HIDra", FileName);
            }
        }
    }

    public static void Write(string what, Exception? error)
    {
        try
        {
            lock (Gate)
            {
                string path = Location;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                var file = new FileInfo(path);
                if (file.Exists && file.Length > MaxBytes)
                {
                    File.Copy(path, path + ".old", overwrite: true);
                    File.Delete(path);
                }

                string version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "?";
                File.AppendAllText(path,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  HIDra {version}  {Environment.MachineName}  {what}{Environment.NewLine}" +
                    $"{error}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch
        {
            // Nowhere to write it: carry on regardless
        }
    }
}
