using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using HIDra.Core.Configuration;
using HIDra.Core.Controllers;
using Microsoft.Win32;

namespace HIDra.UI
{
    /// <summary>
    /// "Check this PC": what HIDra can find out about why it might not work here - the
    /// program's own file, where settings can and cannot go, the controller - as plain
    /// text to read on screen or send to whoever looks after the PCs.
    ///
    /// Only from starting HIDra with --check, never from HIDra's own screens: it is for
    /// whoever looks after the PCs. That runs only this, so it can be used while another
    /// copy is running or when HIDra will not start properly.
    /// </summary>
    public static class PcCheck
    {
        private const string FileName = "HIDra-check.txt";

        /// <summary>Where the last report is saved, beside the settings</summary>
        public static string Location => Path.Combine(UserSettingsStore.Folder, FileName);

        /// <summary>Build the report, save a copy beside the settings, and return it</summary>
        public static string Run()
        {
            var report = new StringBuilder();
            report.AppendLine($"HIDra check, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            Section(report, "HIDra", Program);
            Section(report, "This PC and person", ThisPc);
            Section(report, "Where settings are saved", Settings);
            Section(report, "Controller", Controller);
            Section(report, "Last start-up (HIDra-startup.log)", () => FileText(StartupLog.Location));
            Section(report, "Last errors (HIDra-errors.log)", LastErrors);

            string text = report.ToString();
            try
            {
                File.WriteAllText(Location, text);
            }
            catch
            {
                // Still shown on screen
            }

            return text;
        }

        /// <summary>
        /// One part of the report. A part that fails says so rather than losing the rest.
        /// </summary>
        private static void Section(StringBuilder report, string title, Func<IEnumerable<string>> lines)
        {
            report.AppendLine();
            report.AppendLine(title);
            report.AppendLine(new string('-', title.Length));
            try
            {
                foreach (string line in lines())
                {
                    report.AppendLine(line);
                }
            }
            catch (Exception error)
            {
                report.AppendLine($"Could not check this: {error.Message}");
            }
        }

        private static IEnumerable<string> Program()
        {
            string version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "?";
            string exe = Environment.ProcessPath ?? "?";
            yield return $"Version: {version}";
            yield return $"Program: {exe}";

            // The mark Windows puts on a file downloaded from the internet, which unzipping
            // and copying to a share both keep. On a managed PC it can stop the program
            // being started by double-click or shortcut.
            string? mark = null;
            try
            {
                mark = File.ReadAllText(exe + ":Zone.Identifier");
            }
            catch
            {
                // No mark
            }

            if (mark == null)
            {
                yield return "Downloaded-from-internet mark: none";
            }
            else
            {
                string zone = mark.Split('\n').Select(line => line.Trim())
                    .FirstOrDefault(line => line.StartsWith("ZoneId=", StringComparison.OrdinalIgnoreCase))?[7..] ?? "?";
                yield return $"Downloaded-from-internet mark: YES (zone {zone}{(zone == "3" ? ", the internet" : "")}).";
                yield return "  A managed PC may refuse to start HIDra because of it. To clear it, someone who";
                yield return "  can change the program folder runs, in PowerShell:";
                yield return $"  Get-ChildItem \"{Path.GetDirectoryName(exe)}\" -Recurse | Unblock-File";
            }

            bool running = Mutex.TryOpenExisting(App.InstanceMutexName, out var mutex);
            mutex?.Dispose();
            yield return $"HIDra running for this person: {(running ? "yes" : "no")}";

            string folder = AppContext.BaseDirectory;
            var files = Directory.GetFileSystemEntries(folder).Select(Path.GetFileName).OrderBy(name => name).ToList();
            yield return $"Program folder holds {files.Count} item(s): {string.Join(", ", files.Take(25))}{(files.Count > 25 ? ", ..." : "")}";
        }

        private static IEnumerable<string> ThisPc()
        {
            yield return $"Person: {Environment.UserName} on {Environment.MachineName}";
            yield return $"Windows: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";
            yield return $".NET: {RuntimeInformation.FrameworkDescription}";
            yield return $"%HOMESHARE%: {Variable("HOMESHARE")}";
            yield return $"%HOMEDRIVE%%HOMEPATH%: {Variable("HOMEDRIVE")}{Environment.GetEnvironmentVariable("HOMEPATH")}";
            yield return $"%APPDATA%: {Variable("APPDATA")}";
        }

        private static IEnumerable<string> Settings()
        {
            string overrideFile = UserSettingsStore.OverrideFilePath;
            string? line = UserSettingsStore.OverrideLine;
            if (!File.Exists(overrideFile))
            {
                yield return "HIDra-settings-folder.txt: none beside the program";
                string folder = Path.GetDirectoryName(overrideFile) ?? "";
                if (File.Exists(Path.Combine(folder, "HIDra-settings-folder.txt.txt")))
                {
                    yield return "  But there is a HIDra-settings-folder.txt.txt - rename it to end in .txt once";
                }
            }
            else if (line == null)
            {
                yield return "HIDra-settings-folder.txt: found, but it names no folder (every line is empty or starts with #)";
            }
            else
            {
                yield return $"HIDra-settings-folder.txt: \"{line}\"";
                yield return $"  which for this person is {Environment.ExpandEnvironmentVariables(line)}";
            }

            yield return $"In use: {UserSettingsStore.Folder}";
            yield return "";
            yield return "Each place, tried now, in the order HIDra tries them:";
            foreach (var attempt in UserSettingsStore.CheckAll())
            {
                yield return attempt.Writable
                    ? $"  OK      {attempt.Source}: {attempt.Folder} ({attempt.Milliseconds} ms)"
                    : $"  NO      {attempt.Source}: {attempt.Folder} - {attempt.Problem}";
            }

            yield return "";
            yield return "Files there:";
            var found = Directory.Exists(UserSettingsStore.Folder)
                ? new DirectoryInfo(UserSettingsStore.Folder).GetFiles().OrderBy(file => file.Name).ToList()
                : new List<FileInfo>();
            if (found.Count == 0)
            {
                yield return "  none yet";
            }
            foreach (var file in found)
            {
                yield return $"  {file.Name,-24} {file.Length,8:N0} bytes  changed {file.LastWriteTime:yyyy-MM-dd HH:mm}";
            }
        }

        private static IEnumerable<string> Controller()
        {
            var slots = ControllerCheck.ConnectedSlots();
            yield return slots.Count == 0
                ? "Xbox controller: none found (XInput sees nothing - check it is on, charged, and in XInput mode)"
                : $"Xbox controller: found in slot {string.Join(", ", slots)}";

            object? autoOpen = null;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\TabletTip\1.7");
                autoOpen = key?.GetValue("EnableDesktopModeAutoInvoke");
            }
            catch
            {
                // Reported as not set
            }
            yield return $"Windows keyboard opening by itself: {(autoOpen is int value ? (value == 0 ? "off" : "on") : "Windows default")}";
        }

        private static IEnumerable<string> LastErrors()
        {
            if (!File.Exists(ErrorLog.Location))
            {
                yield return "none";
                yield break;
            }

            // The last few entries are enough; each starts with its date
            var lines = File.ReadAllLines(ErrorLog.Location);
            foreach (string line in lines.Skip(Math.Max(0, lines.Length - 40)))
            {
                yield return line;
            }
        }

        private static IEnumerable<string> FileText(string path) =>
            File.Exists(path) ? File.ReadAllLines(path) : new[] { "none yet" };

        private static string Variable(string name) =>
            Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : "(not set)";
    }
}
