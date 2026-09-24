using HIDra.Models;
using Newtonsoft.Json;

namespace HIDra.Core.Configuration;

/// <summary>
/// Loads and saves each student's settings.
///
/// HIDra is typically run from a network share when a student logs on to any college
/// PC, so the settings have to live somewhere that follows the student rather than the
/// machine. The folder is chosen once at startup, first match wins:
///
///   1. A folder named in HIDra-settings-folder.txt beside HIDra.UI.exe, so IT can point
///      it anywhere. Environment variables are expanded, e.g. H:\HIDra or
///      \\server\hidra-settings\%USERNAME%.
///   2. The student's network home drive, when Windows reports one (%HOMESHARE%\HIDra).
///   3. %APPDATA%\HIDra - which also follows the student if the college uses roaming
///      profiles or folder redirection.
///
/// A location that cannot be reached or written to is skipped, so a missing home drive
/// never stops HIDra starting.
/// </summary>
public static class UserSettingsStore
{
    private const string FileName = "settings.json";
    private const string OverrideFileName = "HIDra-settings-folder.txt";

    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HIDra");

    private static readonly Lazy<string> ChosenFolder = new(ChooseFolder);

    /// <summary>
    /// Where this student's settings are kept, for showing to staff
    /// </summary>
    public static string Location => Path.Combine(ChosenFolder.Value, FileName);

    /// <summary>
    /// The folder this student's files are kept in, so anything else of theirs - such as
    /// their practice - follows them the same way
    /// </summary>
    public static string Folder => ChosenFolder.Value;

    /// <summary>
    /// Load saved settings, falling back to defaults if the file is missing or unreadable
    /// </summary>
    public static UserSettings Load()
    {
        // The first time a new location is used, carry over anything saved in AppData
        // before, so moving the settings somewhere better never loses them.
        // Each file falls back to its backup, so a save cut off part way loses nothing.
        foreach (var path in new[] { Location, Path.Combine(AppDataFolder, FileName) })
        {
            try
            {
                var settings = SafeFile.Read(path, JsonConvert.DeserializeObject<UserSettings>);
                if (settings != null)
                {
                    Normalise(settings);
                    return settings;
                }
            }
            catch
            {
                // A corrupt settings file must never stop the student using the controller
            }
        }

        var defaults = new UserSettings();
        Normalise(defaults);
        return defaults;
    }

    /// <summary>
    /// Save settings. Failures are ignored so the app keeps working.
    /// </summary>
    public static void Save(UserSettings settings)
    {
        try
        {
            Directory.CreateDirectory(ChosenFolder.Value);
            SafeFile.WriteAllText(Location, JsonConvert.SerializeObject(settings, Formatting.Indented));
        }
        catch
        {
            // Not being able to save is not worth interrupting the student for
        }
    }

    /// <summary>
    /// Write a copy of these settings to a file of staff's choosing, to give another
    /// student the same starting point. True if it was written.
    /// </summary>
    public static bool Export(UserSettings settings, string path)
    {
        try
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(settings, Formatting.Indented));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Read settings exported from another student, or null if the file is not settings.
    /// What Windows' own keyboard setting was before HIDra changed it belongs to this
    /// student and this PC, so it is kept rather than copied.
    /// </summary>
    public static UserSettings? Import(string path, UserSettings current)
    {
        try
        {
            var imported = JsonConvert.DeserializeObject<UserSettings>(File.ReadAllText(path));
            if (imported == null)
            {
                return null;
            }

            imported.OriginalWindowsKeyboardAutoInvoke = current.OriginalWindowsKeyboardAutoInvoke;
            Normalise(imported);
            return imported;
        }
        catch
        {
            return null;
        }
    }

    private static void Normalise(UserSettings settings)
    {
        settings.CursorSensitivity = Math.Clamp(settings.CursorSensitivity, 0.05f, 1.0f);
        settings.KeyboardScale = Math.Clamp(settings.KeyboardScale, UserSettings.MinKeyboardScale, UserSettings.MaxKeyboardScale);
        settings.KeyboardFadeOpacity = Math.Clamp(settings.KeyboardFadeOpacity, 0.2f, 0.6f);
        settings.KeyboardFadeSeconds = Math.Clamp(settings.KeyboardFadeSeconds, 1.0f, 10.0f);
        settings.StickSmoothing = Math.Clamp(settings.StickSmoothing, 0, UserSettings.MaxStickSmoothing);
        settings.IgnoreRepeatSeconds = Math.Clamp(settings.IgnoreRepeatSeconds, 0f, 1f);
        settings.SlowPointerPercent = Math.Clamp(settings.SlowPointerPercent, 10, 80);
        // LT used to have its own setting. Carry a student's choice over, so nobody's LT
        // changes under them now that new students start on Escape.
        if (settings.LeftTrigger is LeftTriggerAction old)
        {
            settings.ButtonJobs ??= new Dictionary<string, string>();
            settings.ButtonJobs.TryAdd(ButtonJobCatalogue.LeftTrigger, old switch
            {
                LeftTriggerAction.Escape => "escape",
                LeftTriggerAction.SlowPointer => "slow-pointer",
                LeftTriggerAction.Nothing => ButtonJobCatalogue.Nothing,
                _ => "zoom"
            });
            settings.LeftTrigger = null;
        }

        // A hand-edited or imported file cannot leave a student without a click or a
        // way to open the keyboard
        settings.ButtonJobs = ButtonJobCatalogue.Clean(settings.ButtonJobs);

        // Always exactly one slot per phrase key, whatever an older file held
        settings.Phrases ??= new List<string>();
        while (settings.Phrases.Count < UserSettings.PhraseCount) settings.Phrases.Add("");
        if (settings.Phrases.Count > UserSettings.PhraseCount)
        {
            settings.Phrases.RemoveRange(UserSettings.PhraseCount, settings.Phrases.Count - UserSettings.PhraseCount);
        }
    }

    /// <summary>
    /// How long to wait for a folder on the network to answer. A home drive whose server
    /// is slow or not yet connected - common in the first moments after logon - can keep
    /// Windows trying for tens of seconds, and HIDra's window waits on this choice.
    /// </summary>
    private static readonly TimeSpan NetworkWait = TimeSpan.FromSeconds(5);

    /// <summary>One place settings could be kept, and how trying it went</summary>
    public sealed record FolderAttempt(string Source, string Folder, bool Writable, long Milliseconds, string? Problem);

    private static readonly List<FolderAttempt> ChoiceAttempts = new();

    /// <summary>
    /// The places tried, in order, when the folder was chosen this session - for the
    /// startup log and the check report
    /// </summary>
    public static IReadOnlyList<FolderAttempt> Attempts
    {
        get
        {
            _ = ChosenFolder.Value;
            lock (ChoiceAttempts)
            {
                return ChoiceAttempts.ToList();
            }
        }
    }

    /// <summary>
    /// Try every place again now, not stopping at the first that works, to show which
    /// would and would not do
    /// </summary>
    public static IReadOnlyList<FolderAttempt> CheckAll() =>
        Candidates().Select(candidate => Try(candidate.Source, candidate.Folder)).ToList();

    /// <summary>Where HIDra-settings-folder.txt would be, beside the program</summary>
    public static string OverrideFilePath => Path.Combine(AppContext.BaseDirectory, OverrideFileName);

    /// <summary>
    /// The line HIDra-settings-folder.txt names a folder with, before environment
    /// variables are filled in; null if there is no such file or no such line
    /// </summary>
    public static string? OverrideLine
    {
        get
        {
            try
            {
                return File.Exists(OverrideFilePath)
                    ? File.ReadLines(OverrideFilePath)
                        .Select(line => line.Trim())
                        .FirstOrDefault(line => line.Length > 0 && !line.StartsWith('#'))
                    : null;
            }
            catch
            {
                // An unreadable override file is treated as absent
                return null;
            }
        }
    }

    private static string ChooseFolder()
    {
        // Nothing here may throw: this runs before the window exists, and the choice is
        // remembered for the session, so a failure would stop HIDra starting at all.
        try
        {
            foreach (var (source, folder) in Candidates())
            {
                var attempt = Try(source, folder);
                lock (ChoiceAttempts)
                {
                    ChoiceAttempts.Add(attempt);
                }

                if (attempt.Writable)
                {
                    return folder;
                }
            }
        }
        catch
        {
            // Fall through to AppData
        }

        return AppDataFolder;
    }

    /// <summary>
    /// Whether a folder can be written to, giving up on one that does not answer in time.
    /// The check itself is left to finish in the background.
    /// </summary>
    private static FolderAttempt Try(string source, string folder)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var check = Task.Run(() => WriteProblem(folder));
            if (!check.Wait(NetworkWait))
            {
                return new FolderAttempt(source, folder, false, clock.ElapsedMilliseconds,
                    $"no answer within {NetworkWait.TotalSeconds:0} seconds");
            }

            return new FolderAttempt(source, folder, check.Result == null, clock.ElapsedMilliseconds, check.Result);
        }
        catch (Exception error)
        {
            return new FolderAttempt(source, folder, false, clock.ElapsedMilliseconds, error.Message);
        }
    }

    private static IEnumerable<(string Source, string Folder)> Candidates()
    {
        string? overridden = OverrideLine;
        if (overridden != null)
        {
            yield return (OverrideFileName, Environment.ExpandEnvironmentVariables(overridden));
        }

        string? homeShare = Environment.GetEnvironmentVariable("HOMESHARE");
        if (!string.IsNullOrWhiteSpace(homeShare))
        {
            yield return ("Home drive (%HOMESHARE%)", Path.Combine(homeShare, "HIDra"));
        }

        yield return ("This PC (%APPDATA%)", AppDataFolder);
    }

    /// <summary>Null if a file can be made in the folder, otherwise why not</summary>
    private static string? WriteProblem(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            string probe = Path.Combine(folder, $".write-test-{Environment.ProcessId}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return null;
        }
        catch (Exception error)
        {
            return error.Message;
        }
    }
}
