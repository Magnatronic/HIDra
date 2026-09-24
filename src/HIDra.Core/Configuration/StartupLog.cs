using System.Diagnostics;
using System.Reflection;

namespace HIDra.Core.Configuration;

/// <summary>
/// A few lines about how HIDra's last start went, in HIDra-startup.log beside the
/// person's settings, started afresh each time HIDra starts.
///
/// Its main use is telling two problems apart. If the file's time has not changed since
/// someone logged on, HIDra never ran - Windows refused to start it, or nothing started
/// it - and the answer lies outside HIDra. If it stops part way, it shows how far HIDra
/// got. Writing it must never cause a problem of its own, so every failure is ignored.
/// </summary>
public static class StartupLog
{
    private const string FileName = "HIDra-startup.log";

    private static readonly object Gate = new();

    /// <summary>
    /// When the process began, so the times count from launch - including anything slow
    /// that happened before the first line was written
    /// </summary>
    private static readonly DateTime ProcessStart = WhenProcessStarted();
    private static bool _started;

    /// <summary>Where the log is</summary>
    public static string Location => Path.Combine(UserSettingsStore.Folder, FileName);

    /// <summary>
    /// Start the log for this launch: afresh for the copy that runs, but only a line added
    /// for a second launch, which must not wipe the running copy's record
    /// </summary>
    public static void Begin(bool firstCopy, string[] arguments)
    {
        string version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "?";
        var lines = new List<string>();

        if (firstCopy)
        {
            lines.Add(Line($"HIDra {version} starting"));
            lines.Add(Line($"Program: {Environment.ProcessPath}"));
            lines.Add(Line($"Person: {Environment.UserName} on {Environment.MachineName}"));
            if (arguments.Length > 0)
            {
                lines.Add(Line($"Started with: {string.Join(' ', arguments)}"));
            }

            // Choosing the settings folder is the first thing that can take time, and
            // where this log goes, so what was tried is the next thing recorded
            foreach (var attempt in UserSettingsStore.Attempts)
            {
                lines.Add(Line(attempt.Writable
                    ? $"Settings folder: {attempt.Folder} ({attempt.Source}, {attempt.Milliseconds} ms)"
                    : $"Passed over {attempt.Source}: {attempt.Folder} - {attempt.Problem} ({attempt.Milliseconds} ms)"));
            }
        }
        else
        {
            lines.Add(Line($"A second launch of HIDra {version} found this copy running and asked it to show its window"));
        }

        Write(lines, afresh: firstCopy);
        _started = true;
    }

    /// <summary>Record one step of starting up</summary>
    public static void Step(string what)
    {
        if (_started)
        {
            Write(new[] { Line(what) }, afresh: false);
        }
    }

    private static string Line(string text) =>
        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  +{(DateTime.Now - ProcessStart).TotalMilliseconds,6:0} ms  {text}";

    private static DateTime WhenProcessStarted()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.StartTime;
        }
        catch
        {
            return DateTime.Now;
        }
    }

    private static void Write(IEnumerable<string> lines, bool afresh)
    {
        try
        {
            lock (Gate)
            {
                string text = string.Join(Environment.NewLine, lines) + Environment.NewLine;
                if (afresh)
                {
                    File.WriteAllText(Location, text);
                }
                else
                {
                    File.AppendAllText(Location, text);
                }
            }
        }
        catch
        {
            // Nowhere to write it: carry on regardless
        }
    }
}
