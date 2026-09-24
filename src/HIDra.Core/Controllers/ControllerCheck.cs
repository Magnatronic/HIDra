namespace HIDra.Core.Controllers;

/// <summary>
/// Which controller slots XInput sees right now, for the check report. Asked directly,
/// so it works without the rest of HIDra running.
/// </summary>
public static class ControllerCheck
{
    /// <summary>The XInput slots (1 to 4) with a controller in them</summary>
    public static IReadOnlyList<int> ConnectedSlots()
    {
        var slots = new List<int>();
        try
        {
            for (int i = 0; i < XInputNative.MaxControllerCount; i++)
            {
                if (XInputNative.XInputGetState(i, out _) == XInputNative.ErrorSuccess)
                {
                    slots.Add(i + 1);
                }
            }
        }
        catch
        {
            // XInput missing or refused: reported as no controller
        }

        return slots;
    }
}
