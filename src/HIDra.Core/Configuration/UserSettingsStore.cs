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
    /// Load saved settings, falling back to defaults if the file is missing or unreadable
    /// </summary>
    public static UserSettings Load()
    {
        // The first time a new location is used, carry over anything saved in AppData
        // before, so moving the settings somewhere better never loses them.
        foreach (var path in new[] { Location, Path.Combine(AppDataFolder, FileName) })
        {
            try
            {
                if (File.Exists(path))
                {
                    var settings = JsonConvert.DeserializeObject<UserSettings>(File.ReadAllText(path));
                    if (settings != null)
                    {
                        Normalise(settings);
                        return settings;
                    }
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
            File.WriteAllText(Location, JsonConvert.SerializeObject(settings, Formatting.Indented));
        }
        catch
        {
            // Not being able to save is not worth interrupting the student for
        }
    }

    private static void Normalise(UserSettings settings)
    {
        settings.CursorSensitivity = Math.Clamp(settings.CursorSensitivity, 0.05f, 1.0f);
        settings.KeyboardScale = Math.Clamp(settings.KeyboardScale, UserSettings.MinKeyboardScale, UserSettings.MaxKeyboardScale);
        settings.KeyboardFadeOpacity = Math.Clamp(settings.KeyboardFadeOpacity, 0.2f, 0.6f);
        settings.KeyboardFadeSeconds = Math.Clamp(settings.KeyboardFadeSeconds, 1.0f, 10.0f);

        // Always exactly one slot per phrase key, whatever an older file held
        settings.Phrases ??= new List<string>();
        while (settings.Phrases.Count < UserSettings.PhraseCount) settings.Phrases.Add("");
        if (settings.Phrases.Count > UserSettings.PhraseCount)
        {
            settings.Phrases.RemoveRange(UserSettings.PhraseCount, settings.Phrases.Count - UserSettings.PhraseCount);
        }
    }

    private static string ChooseFolder()
    {
        foreach (var candidate in Candidates())
        {
            if (IsWritable(candidate))
            {
                return candidate;
            }
        }

        return AppDataFolder;
    }

    private static IEnumerable<string> Candidates()
    {
        string? overridden = null;
        try
        {
            string overrideFile = Path.Combine(AppContext.BaseDirectory, OverrideFileName);
            if (File.Exists(overrideFile))
            {
                overridden = File.ReadLines(overrideFile)
                    .Select(line => line.Trim())
                    .FirstOrDefault(line => line.Length > 0 && !line.StartsWith('#'));
            }
        }
        catch
        {
            // An unreadable override file is treated as absent
        }

        if (overridden != null)
        {
            yield return Environment.ExpandEnvironmentVariables(overridden);
        }

        string? homeShare = Environment.GetEnvironmentVariable("HOMESHARE");
        if (!string.IsNullOrWhiteSpace(homeShare))
        {
            yield return Path.Combine(homeShare, "HIDra");
        }

        yield return AppDataFolder;
    }

    private static bool IsWritable(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            string probe = Path.Combine(folder, $".write-test-{Environment.ProcessId}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
