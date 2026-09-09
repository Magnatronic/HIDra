using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using HIDra.Models;

namespace HIDra.UI;

/// <summary>
/// Notification-area icon for HIDra.
///
/// This exists so the window is always recoverable. Closing the window used to exit
/// the application outright, which left the student with no pointer and no way to get
/// it back; now closing hides the window and this icon remains, so either she can use
/// the controller recovery chord or a member of staff can click here.
///
/// The tooltip doubles as an at-a-glance status check for staff at the start of a
/// session, and the balloon warns about a failing battery well before it goes flat.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private bool _lowBatteryWarned;

    /// <summary>Raised when the user asks for the window back.</summary>
    public event EventHandler? ShowRequested;

    /// <summary>Raised when the user genuinely wants HIDra to exit.</summary>
    public event EventHandler? ExitRequested;

    public TrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show HIDra", null, (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit HIDra", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadApplicationIcon(),
            Visible = true,
            Text = "HIDra - starting up",
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Update the hover text to reflect the current connection and battery state.
    /// </summary>
    public void UpdateStatus(bool controllerConnected, ControllerBattery? battery)
    {
        string status = controllerConnected
            ? "Controller connected"
            : "Waiting for controller...";

        if (controllerConnected && battery != null && battery.PowerType != BatteryPowerType.Unknown)
        {
            status += $" - {battery.Description}";
        }

        // NotifyIcon.Text throws above 63 characters on some Windows versions.
        SetTooltip($"HIDra - {status}");
    }

    /// <summary>
    /// Warn once per low-battery episode. Warning repeatedly would train everyone to
    /// ignore it, and the point is that this warning gets acted on.
    /// </summary>
    public void ReportBattery(ControllerBattery battery)
    {
        if (!battery.NeedsAttention)
        {
            _lowBatteryWarned = false;
            return;
        }

        if (_lowBatteryWarned)
        {
            return;
        }

        _lowBatteryWarned = true;

        _notifyIcon.ShowBalloonTip(
            15000,
            "HIDra - controller battery low",
            "The Xbox controller battery is running low. Change or charge it now to avoid losing control of the computer.",
            ToolTipIcon.Warning);
    }

    public void ShowMessage(string title, string message)
    {
        _notifyIcon.ShowBalloonTip(8000, title, message, ToolTipIcon.Info);
    }

    private void SetTooltip(string text)
    {
        const int maxLength = 63;
        _notifyIcon.Text = text.Length <= maxLength
            ? text
            : text[..(maxLength - 3)] + "...";
    }

    private static Icon LoadApplicationIcon()
    {
        try
        {
            string? path = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(path))
            {
                var icon = Icon.ExtractAssociatedIcon(path);
                if (icon != null)
                {
                    return icon;
                }
            }
        }
        catch
        {
            // Fall through to the stock icon below.
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
