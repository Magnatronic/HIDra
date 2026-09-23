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

    /// <summary>Plenty for years of practice, while keeping the file small</summary>
    private const int MaxRuns = 2000;

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
                    progress.PointerRuns ??= new List<PointerRun>();
                    progress.TypingRuns ??= new List<TypingRun>();
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
            Trim(progress.PointerRuns);
            Trim(progress.TypingRuns);
            Directory.CreateDirectory(UserSettingsStore.Folder);
            File.WriteAllText(Location, JsonConvert.SerializeObject(progress, Formatting.Indented));
        }
        catch
        {
            // Not being able to save is not worth interrupting the student for
        }
    }

    private static void Trim<T>(List<T> runs)
    {
        if (runs.Count > MaxRuns)
        {
            runs.RemoveRange(0, runs.Count - MaxRuns);
        }
    }

    /// <summary>
    /// Every run as a spreadsheet (CSV), oldest first, for a review. True if written.
    /// </summary>
    public static bool ExportCsv(PracticeProgress progress, string path, Func<int, string> circleSize, Func<int, string> typingLevel)
    {
        try
        {
            var csv = new StringBuilder();
            csv.AppendLine("Date,Time,Test,Size or level,Seconds,Circles,Misses,Needed a scroll,Scrolled past,Speed score (bits a second),"
                + "Letters,Letters a minute,Wrong letters,Corrections,Keyboard moved,Skipped");

            var rows = new List<(DateTime When, string Line)>();
            foreach (var run in progress.PointerRuns)
            {
                rows.Add((run.When, string.Join(",", run.When.ToString("yyyy-MM-dd,HH:mm", CultureInfo.InvariantCulture),
                    "Pointer", circleSize(run.Size), Number(run.Seconds), run.Targets, run.Misses, run.Scrolled,
                    run.Overshoots, Number(run.Throughput), "", "", "", "", "", "")));
            }
            foreach (var run in progress.TypingRuns)
            {
                rows.Add((run.When, string.Join(",", run.When.ToString("yyyy-MM-dd,HH:mm", CultureInfo.InvariantCulture),
                    "Typing", typingLevel(run.Level), Number(run.Seconds), "", "", "", "", "",
                    run.Characters, Number(run.LettersPerMinute), run.WrongLetters, run.Corrections, run.KeyboardMoves, run.Skipped)));
            }

            rows.Sort((a, b) => a.When.CompareTo(b.When));
            foreach (var row in rows)
            {
                csv.AppendLine(row.Line);
            }

            File.WriteAllText(path, csv.ToString(), Encoding.UTF8);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Number(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);
}
