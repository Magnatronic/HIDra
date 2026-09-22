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
        public sealed record App(string Name, string Path, ImageSource? Icon);

        // In order of how likely a student is to want them: making slides and posters,
        // then research, then finding files
        private static readonly (string Name, string Exe)[] Candidates =
        {
            ("PowerPoint", "POWERPNT.EXE"),
            ("Word", "WINWORD.EXE"),
            ("Publisher", "MSPUB.EXE"),
            ("Edge", "msedge.exe"),
            ("Chrome", "chrome.exe"),
            ("File Explorer", "explorer.exe"),
        };

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
                    found.Add(new App(name, path, IconOf(path)));
                }
            }

            return found;
        }

        private static string? Resolve(string exe)
        {
            if (exe.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
            {
                string explorer = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
                return File.Exists(explorer) ? explorer : null;
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
