using System;
using System.Runtime.Versioning;

namespace HIDra.Core.Controllers;

/// <summary>
/// Explains why no controller was found, when a controller is plainly plugged in.
///
/// HIDra drives everything through XInput, which is blind to gamepads running in
/// DirectInput mode. Without this check, such a controller and no controller at all
/// produce the same "waiting for a controller" message, so a member of staff has no way
/// to tell a wrong-mode switch from a flat battery from a broken application.
///
/// Windows.Gaming.Input is used here purely as a sensor, never as an input path: it can
/// see the controllers XInput cannot, but it reports them as anonymous numbered buttons
/// with no idea which one is "A". That is enough to explain the problem, and far short
/// of what would be needed to actually drive the machine from one.
/// </summary>
public static class ControllerDiagnostics
{
    /// <summary>
    /// Windows.Gaming.Input starts enumerating devices when the list is first touched
    /// and fills it in asynchronously, so the very first read always comes back empty
    /// even with a controller plugged in. That first read is treated as a warm-up and
    /// deliberately reports nothing, rather than reporting a wrong answer confidently.
    /// </summary>
    private static bool _enumerationPrimed;

    /// <summary>
    /// Look for a gamepad that Windows can see but XInput cannot.
    ///
    /// Only meaningful when XInput has already reported nothing: in that situation any
    /// controller Windows still knows about is, by definition, one HIDra cannot use.
    /// Returns null when there is genuinely nothing attached, and on the first call.
    /// </summary>
    [SupportedOSPlatform("windows10.0.17763.0")]
    public static NonXInputController? FindControllerXInputCannotUse()
    {
        try
        {
            var controllers = Windows.Gaming.Input.RawGameController.RawGameControllers;

            if (!_enumerationPrimed)
            {
                // Touching the list above is what starts enumeration. Whatever it says
                // right now cannot be trusted, so wait to be asked again.
                _enumerationPrimed = true;
                return null;
            }

            if (controllers.Count == 0)
            {
                return null;
            }

            var controller = controllers[0];

            return new NonXInputController
            {
                DisplayName = string.IsNullOrWhiteSpace(controller.DisplayName)
                    ? "Game controller"
                    : controller.DisplayName,
                VendorId = controller.HardwareVendorId,
                ProductId = controller.HardwareProductId,
                ButtonCount = controller.ButtonCount,
                AxisCount = controller.AxisCount
            };
        }
        catch (Exception)
        {
            // The diagnostic must never be the reason HIDra misbehaves. If Windows
            // cannot answer, we simply do not offer an explanation.
            return null;
        }
    }
}

/// <summary>
/// A controller Windows can see but XInput cannot drive.
/// </summary>
public class NonXInputController
{
    public string DisplayName { get; set; } = string.Empty;

    public ushort VendorId { get; set; }

    public ushort ProductId { get; set; }

    public int ButtonCount { get; set; }

    public int AxisCount { get; set; }

    /// <summary>
    /// Wording aimed at whoever is stood in front of the machine trying to make it
    /// work, rather than at a developer reading a log.
    /// </summary>
    public string Explanation =>
        $"A controller is connected ({DisplayName}) but it is not in XInput mode, " +
        "so HIDra cannot use it. Switch the controller to XInput mode - on many pads " +
        "this is a physical switch, often marked 2.4G, X or XInput.";

    /// <summary>Identifiers, for a support call where the pad model matters.</summary>
    public string TechnicalDetail =>
        $"VID_{VendorId:X4} PID_{ProductId:X4}, {ButtonCount} buttons, {AxisCount} axes";
}
