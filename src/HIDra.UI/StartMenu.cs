using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HIDra.UI
{
    /// <summary>
    /// Every program on the Start menu, read from Windows' own list of them (the Apps
    /// folder) - desktop programs and Store apps alike, with the icons Windows shows for
    /// them. Store apps, like the new Outlook, are not in the registry the standard
    /// programs are found in, so this is the only way to offer them.
    /// </summary>
    internal static class StartMenu
    {
        private const string AppsFolder = "shell:AppsFolder";

        /// <summary>
        /// Entries that are not programs to open: uninstallers, help files, web links
        /// </summary>
        private static readonly string[] NotPrograms =
        {
            "uninstall", "help", "readme", "read me", "documentation", "release notes",
            "manual", "licen", "website", "support", "what's new"
        };

        private static readonly Dictionary<string, AppLauncher.App?> Cache = new();

        /// <summary>
        /// Every program on the Start menu, by name. Empty if Windows will not say.
        /// </summary>
        public static List<AppLauncher.App> All()
        {
            var apps = new List<AppLauncher.App>();
            try
            {
                var shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null)
                {
                    return apps;
                }

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic folder = shell.NameSpace(AppsFolder);
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (dynamic item in folder.Items())
                {
                    string name = item.Name;
                    string parsingName = item.Path;
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(parsingName)
                        || !IsProgram(name, parsingName) || !seen.Add(name))
                    {
                        continue;
                    }

                    var app = new AppLauncher.App(AppLauncher.StartPrefix + parsingName, name,
                        $@"{AppsFolder}\{parsingName}", IconOf(parsingName));
                    Cache[app.Id] = app;
                    apps.Add(app);
                }
            }
            catch
            {
                // No Start menu list is no worse than the standard programs alone
            }

            apps.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return apps;
        }

        /// <summary>
        /// A program saved in a student's settings, looked up by itself rather than by
        /// reading the whole Start menu, which is slower. Null if it is no longer there.
        /// </summary>
        public static AppLauncher.App? FromId(string id)
        {
            if (Cache.TryGetValue(id, out var cached))
            {
                return cached;
            }

            AppLauncher.App? app = null;
            try
            {
                string parsingName = id[AppLauncher.StartPrefix.Length..];
                SHCreateItemFromParsingName($@"{AppsFolder}\{parsingName}", IntPtr.Zero,
                    typeof(IShellItem).GUID, out var item);
                ((IShellItem)item).GetDisplayName(0, out var name);
                Marshal.ReleaseComObject(item);
                app = new AppLauncher.App(id, name, $@"{AppsFolder}\{parsingName}", IconOf(parsingName));
            }
            catch
            {
                // Uninstalled since it was chosen: its place shows as empty
            }

            Cache[id] = app;
            return app;
        }

        private static bool IsProgram(string name, string parsingName)
        {
            string lower = name.ToLowerInvariant();
            foreach (var word in NotPrograms)
            {
                if (lower.Contains(word))
                {
                    return false;
                }
            }

            string path = parsingName.ToLowerInvariant();
            return !(path.StartsWith("http") || path.EndsWith(".url") || path.EndsWith(".txt")
                || path.EndsWith(".chm") || path.EndsWith(".pdf") || path.EndsWith(".htm") || path.EndsWith(".html"));
        }

        /// <summary>
        /// The icon Windows shows for it on the Start menu
        /// </summary>
        private static ImageSource? IconOf(string parsingName)
        {
            IntPtr bitmap = IntPtr.Zero;
            object? item = null;
            try
            {
                SHCreateItemFromParsingName($@"{AppsFolder}\{parsingName}", IntPtr.Zero,
                    typeof(IShellItemImageFactory).GUID, out item);
                if (((IShellItemImageFactory)item).GetImage(new SIZE { cx = 48, cy = 48 }, SIIGBF_ICONONLY, out bitmap) != 0)
                {
                    return null;
                }

                return FromBitmap(bitmap);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (bitmap != IntPtr.Zero)
                {
                    DeleteObject(bitmap);
                }
                if (item != null)
                {
                    Marshal.ReleaseComObject(item);
                }
            }
        }

        /// <summary>
        /// Copy the icon's pixels with their transparency. WPF's own conversion from a
        /// bitmap handle drops it, leaving each icon on a black square.
        /// </summary>
        private static ImageSource? FromBitmap(IntPtr bitmap)
        {
            var section = new DIBSECTION();
            if (GetObject(bitmap, Marshal.SizeOf<DIBSECTION>(), ref section) == 0
                || section.dsBm.bmBits == IntPtr.Zero || section.dsBm.bmBitsPixel != 32)
            {
                var plain = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero,
                    Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                plain.Freeze();
                return plain;
            }

            int width = section.dsBm.bmWidth, height = section.dsBm.bmHeight, stride = section.dsBm.bmWidthBytes;
            BitmapSource image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null,
                section.dsBm.bmBits, stride * height, stride);

            // Rows stored bottom first are the right way up only once flipped
            if (section.dsBmih.biHeight > 0)
            {
                image = new TransformedBitmap(image, new ScaleTransform(1, -1));
            }

            image.Freeze();
            return image;
        }

        private const int SIIGBF_ICONONLY = 0x4;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(string path, IntPtr bindContext,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.Interface)] out object item);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);

        [DllImport("gdi32.dll")]
        private static extern int GetObject(IntPtr handle, int size, ref DIBSECTION section);

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DIBSECTION
        {
            public BITMAP dsBm;
            public BITMAPINFOHEADER dsBmih;
            public uint dsBitfields0;
            public uint dsBitfields1;
            public uint dsBitfields2;
            public IntPtr dshSection;
            public uint dsOffset;
        }

        [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr bindContext, ref Guid handler, ref Guid riid, out IntPtr result);
            void GetParent(out IntPtr parent);
            void GetDisplayName(uint form, [MarshalAs(UnmanagedType.LPWStr)] out string name);
        }

        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(SIZE size, int flags, out IntPtr bitmap);
        }
    }
}
