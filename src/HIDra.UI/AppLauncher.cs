using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace HIDra.UI
{
    /// <summary>
    /// The programs the keyboard's Apps key can open in one press. Reaching them through
    /// the Start menu with a controller is a long string of deliberate movements.
    ///
    /// A new student starts with a standard few, found the way Windows itself finds them
    /// (the App Paths registry). Staff can choose any program on the Start menu instead -
    /// Store apps too, such as the new Outlook, which the registry never lists. Each is
    /// shown with its own icon: the PowerPoint logo is recognisable at a glance, which
    /// matters for a student who relies on pictures more than words.
    /// </summary>
    public static class AppLauncher
    {
        /// <summary>
        /// A program that can be offered. Its id, the program's file name in lower case,
        /// is what a student's settings save.
        /// </summary>
        public sealed record App(string Id, string Name, string Path, ImageSource? Icon);

        // Programs found the way Windows finds them. Any other program on the Start menu
        // can be chosen too (StartMenu).
        private static readonly (string Name, string Exe)[] Candidates =
        {
            ("Edge", "msedge.exe"),
            ("Word", "WINWORD.EXE"),
            ("PowerPoint", "POWERPNT.EXE"),
            ("Outlook", "OUTLOOK.EXE"),
            ("File Explorer", "explorer.exe"),
            ("Excel", "EXCEL.EXE"),
            ("OneNote", "ONENOTE.EXE"),
            ("Publisher", "MSPUB.EXE"),
            ("Chrome", "chrome.exe"),
            ("Firefox", "firefox.exe"),
            ("Notepad", "notepad.exe"),
            ("Calculator", "calc.exe"),
        };

        /// <summary>
        /// What the Apps key offers before any choice is made, place by place: the web,
        /// writing, slides, email, files and Teams - what a college day is mostly made of.
        /// Each place takes the first of its programs that is on this PC, or stays empty.
        /// Outlook and Teams are often Store apps, found by Windows' own name for them.
        /// </summary>
        private static readonly string[][] DefaultPlaces =
        {
            new[] { "msedge.exe" },
            new[] { "winword.exe" },
            new[] { "powerpnt.exe" },
            new[] { "outlook.exe", StartPrefix + "Microsoft.OutlookForWindows_8wekyb3d8bbwe!Microsoft.OutlookforWindows" },
            new[] { "explorer.exe" },
            new[] { StartPrefix + "MSTeams_8wekyb3d8bbwe!MSTeams" },
        };

        /// <summary>
        /// The chosen programs, one per slot, null where a slot is empty or the program is
        /// not on this PC. No choice saved means the default places.
        /// </summary>
        public static App?[] Chosen(IList<string>? ids, int slots)
        {
            var chosen = new App?[slots];

            if (ids == null)
            {
                for (int i = 0; i < slots && i < DefaultPlaces.Length; i++)
                {
                    foreach (var id in DefaultPlaces[i])
                    {
                        if (Find(id) is App app)
                        {
                            chosen[i] = app;
                            break;
                        }
                    }
                }
                return chosen;
            }

            for (int i = 0; i < slots && i < ids.Count; i++)
            {
                chosen[i] = Find(ids[i]);
            }
            return chosen;
        }

        public static App? Find(string? id)
        {
            foreach (var app in Installed)
            {
                if (app.Id == id)
                {
                    return app;
                }
            }

            return id != null && id.StartsWith(StartPrefix) ? StartMenu.FromId(id) : null;
        }

        /// <summary>
        /// What staff can choose from: the standard programs, then everything else on the
        /// Start menu, by name
        /// </summary>
        public static IReadOnlyList<App> Choices => _choices ??= ListChoices();

        private static IReadOnlyList<App>? _choices;

        private static IReadOnlyList<App> ListChoices()
        {
            var choices = new List<App>(Installed);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var app in Installed)
            {
                names.Add(app.Name);
            }

            foreach (var app in StartMenu.All())
            {
                if (names.Add(app.Name))
                {
                    choices.Add(app);
                }
            }

            return choices;
        }

        /// <summary>Ids of Start menu programs begin with this; the rest is Windows' own name for it</summary>
        internal const string StartPrefix = "start:";

        private static IReadOnlyList<App>? _installed;

        /// <summary>
        /// The installed programs, worked out once - looking them up means registry reads
        /// and icon extraction, which need not be repeated every time the key is pressed
        /// </summary>
        public static IReadOnlyList<App> Installed => _installed ??= Find();

        public static void Launch(App app)
        {
            try
            {
                // A Start menu program is opened the way the Start menu opens it, which is
                // the only way for a Store app
                var start = app.Id.StartsWith(StartPrefix)
                    ? new ProcessStartInfo("explorer.exe", app.Path)
                    : new ProcessStartInfo(app.Path) { UseShellExecute = true };
                Process.Start(start);
            }
            catch
            {
                // A blocked or broken program must not take HIDra down with it
            }
        }

        private static IReadOnlyList<App> Find()
        {
            var found = new List<App>();

            foreach (var (name, exe) in Candidates)
            {
                string? path = Resolve(exe);
                if (path != null)
                {
                    found.Add(new App(exe.ToLowerInvariant(), name, path, IconOf(path)));
                }
            }

            return found;
        }

        private static string? Resolve(string exe)
        {
            // Parts of Windows itself, which are not listed where installed programs are
            foreach (var folder in new[] { Environment.SpecialFolder.Windows, Environment.SpecialFolder.System })
            {
                if (exe is "explorer.exe" or "notepad.exe" or "calc.exe")
                {
                    string windowsProgram = System.IO.Path.Combine(Environment.GetFolderPath(folder), exe);
                    if (File.Exists(windowsProgram))
                    {
                        return windowsProgram;
                    }
                }
            }

            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    using var key = hive.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\App Paths\{exe}");
                    if (key?.GetValue(null) is string value)
                    {
                        string path = Environment.ExpandEnvironmentVariables(value.Trim('"'));
                        if (File.Exists(path))
                        {
                            return path;
                        }
                    }
                }
                catch
                {
                    // An unreadable key is the same as a missing one
                }
            }

            return null;
        }

        private static ImageSource? IconOf(string path)
        {
            try
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon == null)
                {
                    return null;
                }

                var image = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }
    }
}
