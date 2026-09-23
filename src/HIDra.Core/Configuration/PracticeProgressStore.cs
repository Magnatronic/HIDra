using System.Globalization;
using System.Text;
using HIDra.Models;
using Newtonsoft.Json;

namespace HIDra.Core.Configuration;

/// <summary>
/// Loads and saves a student's practice, in practice.json beside their settings, so it
/// follows them from PC to PC as the settings do. Kept apart from the settings on
/// purpose: exporting settings to another student must not hand them this one's record.
/// </summary>
public static class PracticeProgressStore
{
    private const string FileName = "practice.json";

    /// <summary>About two school years of days with practice</summary>
    private const int MaxDays = 400;

    public static string Location => Path.Combine(UserSettingsStore.Folder, FileName);

    /// <summary>
    /// The practice so far, or a fresh record if there is none or it cannot be read
    /// </summary>
    public static PracticeProgress Load()
    {
        try
        {
            if (File.Exists(Location))
            {
                var progress = JsonConvert.DeserializeObject<PracticeProgress>(File.ReadAllText(Location));
                if (progress != null)
                {
                    progress.Days ??= new List<PracticeDay>();
                    return progress;
                }
            }
        }
        catch
        {
            // A damaged record must never stop the student practising
        }

        return new PracticeProgress();
    }

    /// <summary>
    /// Save. Failures are ignored, as for settings.
    /// </summary>
    public static void Save(PracticeProgress progress)
    {
        try
        {
            if (progress.Days.Count > MaxDays)
            {
                progress.Days.RemoveRange(0, progress.Days.Count - MaxDays);
            }

            Directory.CreateDirectory(UserSettingsStore.Folder);
            File.WriteAllText(Location, JsonConvert.SerializeObject(progress, Formatting.Indented));
        }
        catch
        {
            // Not being able to save is not worth interrupting the student for
        }
    }

    /// <summary>
    /// Today's entry, added if this is the first practice today
    /// </summary>
    public static PracticeDay Today(PracticeProgress progress)
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (progress.Days.Count > 0 && progress.Days[^1].Date == today)
        {
            return progress.Days[^1];
        }

        var day = new PracticeDay { Date = today, SmallestCircle = progress.CircleLevel };
        progress.Days.Add(day);
        return day;
    }

    /// <summary>
    /// Write every day as a spreadsheet (CSV), for a review. True if it was written.
    /// </summary>
    public static bool ExportCsv(PracticeProgress progress, string path, Func<int, string> circleSize)
    {
        try
        {
            var csv = new StringBuilder();
            csv.AppendLine("Date,Circles hit,Circles missed,Smallest circle,Words typed,Wrong letters,Keyboard opened and closed");
            foreach (var day in progress.Days)
            {
                csv.AppendLine(string.Join(",", day.Date, day.CircleHits, day.CircleMisses,
                    circleSize(day.SmallestCircle), day.WordsTyped, day.WrongLetters, day.KeyboardRounds));
            }

            File.WriteAllText(path, csv.ToString(), Encoding.UTF8);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
