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
    /// Only programs actually installed on this PC are offered, found the way Windows
    /// itself finds them (the App Paths registry), and each is shown with its own icon -
    /// the PowerPoint logo is recognisable at a glance, which matters for a student who
    /// relies on pictures more than words.
    /// </summary>
    public static class AppLauncher
    {
        /// <summary>
        /// A program that can be offered. Its id, the program's file name in lower case,
        /// is what a student's settings save.
        /// </summary>
        public sealed record App(string Id, string Name, string Path, ImageSource? Icon);

        // The first six, in this order, are what a student starts with: making slides and
        // posters, then research, then finding files. Staff can choose any of the rest.
        private static readonly (string Name, string Exe)[] Candidates =
        {
            ("PowerPoint", "POWERPNT.EXE"),
            ("Word", "WINWORD.EXE"),
            ("Publisher", "MSPUB.EXE"),
            ("Edge", "msedge.exe"),
            ("Chrome", "chrome.exe"),
            ("File Explorer", "explorer.exe"),
            ("Excel", "EXCEL.EXE"),
            ("OneNote", "ONENOTE.EXE"),
            ("Outlook", "OUTLOOK.EXE"),
            ("Firefox", "firefox.exe"),
            ("Notepad", "notepad.exe"),
            ("Calculator", "calc.exe"),
        };

        /// <summary>
        /// A student's chosen programs, one per slot, null where a slot is empty or the
        /// program is not installed on this PC. No choice saved means the first installed
        /// programs in the standard order.
        /// </summary>
        public static App?[] Chosen(IList<string>? ids, int slots)
        {
            var chosen = new App?[slots];

            if (ids == null)
            {
                for (int i = 0; i < slots && i < Installed.Count; i++)
                {
                    chosen[i] = Installed[i];
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
            return null;
        }

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
                Process.Start(new ProcessStartInfo(app.Path) { UseShellExecute = true });
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
