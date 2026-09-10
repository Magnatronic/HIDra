using HIDra.Models;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace HIDra.Core.Controllers;

/// <summary>
/// Owns the connection to the Xbox controller and publishes its state.
///
/// The single most important property of this class is that it never gives up. The
/// student it was written for has no other way to use the machine, so a flat battery,
/// a Bluetooth dropout or a nudged cable must recover on its own - a version that
/// stopped polling and waited for someone to click "Reconnect" with a mouse left her
/// stranded until a member of staff walked over.
///
/// The supervision loop therefore runs from Start() to Stop() and exits for no other
/// reason: when no controller is present it scans for one, and when the controller
/// goes away it goes back to scanning rather than terminating.
/// </summary>
public class XboxControllerService : IDisposable
{
    /// <summary>
    /// How often to sweep all four XInput slots while no controller is attached.
    ///
    /// Microsoft advises against probing empty slots every frame because a call
    /// against an absent device is comparatively slow. Twice a second is a deliberate
    /// compromise: far below "every frame", but fast enough that a controller coming
    /// back feels immediate to someone who is waiting on it.
    /// </summary>
    private const int SearchIntervalMs = 500;

    /// <summary>
    /// Battery level changes over hours, not milliseconds, so it is sampled sparingly.
    /// </summary>
    private const int BatteryPollIntervalMs = 30_000;

    /// <summary>Backstop delay after an unexpected error, so a persistent fault cannot spin the CPU.</summary>
    private const int ErrorBackoffMs = 250;

    /// <summary>
    /// How often, while searching, to ask Windows whether a controller is attached that
    /// XInput cannot see. Far less often than the search itself - this only needs to
    /// answer a question someone is asking after several fruitless seconds.
    /// </summary>
    private const int DiagnosticIntervalMs = 2000;

    private CancellationTokenSource? _cts;
    private Task? _supervisorTask;

    private int _attachedIndex = -1;
    private ControllerInfo? _controllerInfo;
    private ControllerBattery? _battery;
    private bool _unusableControllerReported;

    /// <summary>Raised on every successful poll of an attached controller.</summary>
    public event EventHandler<ControllerState>? StateUpdated;

    /// <summary>Raised when the controller is attached or lost.</summary>
    public event EventHandler<ControllerInfo>? ConnectionChanged;

    /// <summary>Raised when the battery power source or charge level changes.</summary>
    public event EventHandler<ControllerBattery>? BatteryChanged;

    /// <summary>
    /// Raised when a controller is attached that XInput cannot use, so the UI can say
    /// what is wrong instead of leaving someone staring at "waiting for a controller".
    /// Raised once per episode, and again only if the situation recurs.
    /// </summary>
    public event EventHandler<NonXInputController>? UnusableControllerDetected;

    public ControllerInfo? Controller => _controllerInfo;

    public ControllerBattery? Battery => _battery;

    public bool IsAttached => _attachedIndex >= 0;

    /// <summary>
    /// Look for a controller once, without starting the supervision loop.
    /// Used at startup so the UI can report immediately whether one is present.
    /// </summary>
    public ControllerInfo? DetectController()
    {
        int index = FindConnectedControllerIndex();
        return index < 0 ? null : BuildControllerInfo(index);
    }

    /// <summary>
    /// Start supervising the controller. Safe to call when nothing is plugged in -
    /// the loop will pick a controller up as soon as one appears.
    /// </summary>
    public void StartPolling(int pollRateMs)
    {
        if (_supervisorTask != null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _supervisorTask = Task.Run(() => SuperviseAsync(pollRateMs, _cts.Token));
    }

    public void StopPolling()
    {
        _cts?.Cancel();

        try
        {
            _supervisorTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Cancellation surfaces here and is expected.
        }

        _supervisorTask = null;
        _cts?.Dispose();
        _cts = null;

        if (_attachedIndex >= 0)
        {
            // Silent teardown: nothing was lost, we were asked to stop. Announcing a
            // disconnection here made the UI report "controller lost - searching for
            // it" immediately after a deliberate stop, which was simply untrue.
            Detach(announce: false);
        }
    }

    /// <summary>
    /// The supervision loop. This returns only when cancelled; every other path
    /// loops back round, including errors we did not anticipate.
    /// </summary>
    private async Task SuperviseAsync(int pollRateMs, CancellationToken token)
    {
        var batteryStopwatch = Stopwatch.StartNew();

        // The first diagnostic check only warms up Windows' device enumeration, so the
        // real answer arrives on the second - roughly four seconds into a fruitless
        // search, which is about when someone starts wondering why nothing is happening.
        var diagnosticStopwatch = Stopwatch.StartNew();

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (_attachedIndex < 0)
                {
                    int index = FindConnectedControllerIndex();

                    if (index < 0)
                    {
                        // Nothing XInput can use. Before waiting, occasionally check
                        // whether something is attached that simply is not in XInput
                        // mode, so the UI can explain rather than just keep waiting.
                        if (diagnosticStopwatch.ElapsedMilliseconds >= DiagnosticIntervalMs)
                        {
                            diagnosticStopwatch.Restart();
                            CheckForUnusableController();
                        }

                        await Task.Delay(SearchIntervalMs, token).ConfigureAwait(false);
                        continue;
                    }

                    // A usable controller arrived, so any previous explanation is stale
                    // and the warning should be allowed to fire again in future.
                    _unusableControllerReported = false;

                    Attach(index);
                    batteryStopwatch.Restart();
                    UpdateBattery();
                }

                int result = XInputNative.XInputGetState(_attachedIndex, out var nativeState);

                if (result == XInputNative.ErrorDeviceNotConnected)
                {
                    // The controller went away. Drop back to scanning rather than
                    // stopping - this is the case that used to strand the user.
                    Detach();
                    continue;
                }

                if (result != XInputNative.ErrorSuccess)
                {
                    // An unexpected status. Treat it like a transient fault and retry,
                    // but always after a delay so we cannot spin.
                    await Task.Delay(ErrorBackoffMs, token).ConfigureAwait(false);
                    continue;
                }

                StateUpdated?.Invoke(this, ParseControllerState(nativeState.Gamepad));

                if (batteryStopwatch.ElapsedMilliseconds >= BatteryPollIntervalMs)
                {
                    batteryStopwatch.Restart();
                    UpdateBattery();
                }

                await Task.Delay(pollRateMs, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Stop() was called - the only legitimate way out of this loop.
                break;
            }
            catch (Exception)
            {
                // Deliberately swallowed: no fault in a handler or in XInput itself is
                // worth taking the controller away from someone who depends on it. The
                // delay below is what keeps this from becoming a busy loop, which is
                // exactly what the previous implementation degraded into.
                try
                {
                    await Task.Delay(ErrorBackoffMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Sweep all four XInput slots and return the first connected one, or -1.
    /// Sweeping every slot rather than remembering the old one means the controller
    /// can come back on a different slot - which is what happens in practice after a
    /// Bluetooth reconnect.
    /// </summary>
    private static int FindConnectedControllerIndex()
    {
        for (int i = 0; i < XInputNative.MaxControllerCount; i++)
        {
            if (XInputNative.XInputGetState(i, out _) == XInputNative.ErrorSuccess)
            {
                return i;
            }
        }

        return -1;
    }

    private void Attach(int index)
    {
        _attachedIndex = index;
        _controllerInfo = BuildControllerInfo(index);
        ConnectionChanged?.Invoke(this, _controllerInfo);
    }

    /// <summary>
    /// Release the attached controller. <paramref name="announce"/> is false when the
    /// caller asked us to stop, so a deliberate shutdown is not reported as a loss.
    /// </summary>
    private void Detach(bool announce = true)
    {
        _attachedIndex = -1;

        if (_controllerInfo != null)
        {
            _controllerInfo.Status = ConnectionStatus.Disconnected;

            if (announce)
            {
                ConnectionChanged?.Invoke(this, _controllerInfo);
            }
        }

        if (_battery != null)
        {
            _battery = new ControllerBattery();

            if (announce)
            {
                BatteryChanged?.Invoke(this, _battery);
            }
        }
    }

    private static ControllerInfo BuildControllerInfo(int index) => new()
    {
        DeviceId = $"XInput_{index}",
        Type = ControllerType.XboxOne,
        Name = "Xbox Controller",
        VendorId = 0x045E,
        ProductId = 0x028E,
        Status = ConnectionStatus.Connected
    };

    /// <summary>
    /// Ask Windows whether a controller is present that XInput cannot drive, and report
    /// it once. Reporting repeatedly would train everyone to ignore the message.
    /// </summary>
    private void CheckForUnusableController()
    {
        if (_unusableControllerReported)
        {
            return;
        }

        var unusable = ControllerDiagnostics.FindControllerXInputCannotUse();

        if (unusable == null)
        {
            return;
        }

        _unusableControllerReported = true;
        UnusableControllerDetected?.Invoke(this, unusable);
    }

    private void UpdateBattery()
    {
        if (_attachedIndex < 0)
        {
            return;
        }

        int result = XInputNative.XInputGetBatteryInformation(
            _attachedIndex,
            XInputNative.BatteryDeviceTypeGamepad,
            out var info);

        if (result != XInputNative.ErrorSuccess)
        {
            return;
        }

        var battery = new ControllerBattery
        {
            PowerType = info.BatteryType switch
            {
                XInputNative.BatteryTypes.Wired => BatteryPowerType.Wired,
                XInputNative.BatteryTypes.Alkaline => BatteryPowerType.Alkaline,
                XInputNative.BatteryTypes.NiMh => BatteryPowerType.Rechargeable,
                _ => BatteryPowerType.Unknown
            },
            ChargeLevel = info.BatteryLevel switch
            {
                XInputNative.BatteryLevels.Empty => BatteryChargeLevel.Empty,
                XInputNative.BatteryLevels.Low => BatteryChargeLevel.Low,
                XInputNative.BatteryLevels.Medium => BatteryChargeLevel.Medium,
                XInputNative.BatteryLevels.Full => BatteryChargeLevel.Full,
                _ => BatteryChargeLevel.Unknown
            }
        };

        if (battery.SameAs(_battery))
        {
            return;
        }

        _battery = battery;
        BatteryChanged?.Invoke(this, battery);
    }

    private static ControllerState ParseControllerState(XInputNative.XInputGamepad gamepad)
    {
        var buttons = (XInputNative.GamepadButtons)gamepad.Buttons;

        bool Pressed(XInputNative.GamepadButtons flag) => (buttons & flag) != 0;

        var state = new ControllerState
        {
            ButtonA = Pressed(XInputNative.GamepadButtons.A),
            ButtonB = Pressed(XInputNative.GamepadButtons.B),
            ButtonX = Pressed(XInputNative.GamepadButtons.X),
            ButtonY = Pressed(XInputNative.GamepadButtons.Y),
            LeftBumper = Pressed(XInputNative.GamepadButtons.LeftShoulder),
            RightBumper = Pressed(XInputNative.GamepadButtons.RightShoulder),
            Start = Pressed(XInputNative.GamepadButtons.Start),
            Back = Pressed(XInputNative.GamepadButtons.Back),
            LeftStickClick = Pressed(XInputNative.GamepadButtons.LeftThumb),
            RightStickClick = Pressed(XInputNative.GamepadButtons.RightThumb),
            DpadUp = Pressed(XInputNative.GamepadButtons.DPadUp),
            DpadDown = Pressed(XInputNative.GamepadButtons.DPadDown),
            DpadLeft = Pressed(XInputNative.GamepadButtons.DPadLeft),
            DpadRight = Pressed(XInputNative.GamepadButtons.DPadRight),

            // Triggers report 0-255.
            LeftTrigger = gamepad.LeftTrigger / 255f,
            RightTrigger = gamepad.RightTrigger / 255f,

            // Sticks report signed 16-bit values. Dividing by 32767 and clamping keeps
            // the -32768 end of the range from exceeding -1.0.
            LeftStickX = Math.Clamp(gamepad.ThumbLX / 32767f, -1f, 1f),
            LeftStickY = Math.Clamp(gamepad.ThumbLY / 32767f, -1f, 1f),
            RightStickX = Math.Clamp(gamepad.ThumbRX / 32767f, -1f, 1f),
            RightStickY = Math.Clamp(gamepad.ThumbRY / 32767f, -1f, 1f)
        };

        return state;
    }

    public void Dispose()
    {
        StopPolling();
        _controllerInfo = null;
        _battery = null;
    }
}
