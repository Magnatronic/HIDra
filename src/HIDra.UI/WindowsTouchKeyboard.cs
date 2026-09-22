using HIDra.Models;
using Microsoft.Win32;

namespace HIDra.UI
{
    /// <summary>
    /// Stops Windows opening its own touch/gamepad keyboard whenever a text box gets
    /// focus. With a controller connected, that keyboard takes over the controller, so
    /// HIDra and Windows both react to the same sticks and the student loses the cursor.
    ///
    /// Only the current user's setting is changed, and the value it had before is kept
    /// so turning the option off puts back exactly what the student had.
    /// </summary>
    public static class WindowsTouchKeyboard
    {
        private const string KeyPath = @"Software\Microsoft\TabletTip\1.7";
        private const string ValueName = "EnableDesktopModeAutoInvoke";

        /// <summary>
        /// Apply the student's choice. Returns true if the saved settings changed and
        /// need writing.
        /// </summary>
        public static bool Apply(UserSettings settings)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
                int current = key.GetValue(ValueName) as int? ?? 1;

                if (settings.StopWindowsKeyboard)
                {
                    if (current == 0)
                    {
                        return false;
                    }

                    settings.OriginalWindowsKeyboardAutoInvoke ??= current;
                    key.SetValue(ValueName, 0, RegistryValueKind.DWord);
                    return true;
                }

                if (settings.OriginalWindowsKeyboardAutoInvoke is int original)
                {
                    key.SetValue(ValueName, original, RegistryValueKind.DWord);
                    settings.OriginalWindowsKeyboardAutoInvoke = null;
                    return true;
                }
            }
            catch
            {
                // A locked-down machine may refuse; HIDra still works, the pop-up just remains
            }

            return false;
        }
    }
}
