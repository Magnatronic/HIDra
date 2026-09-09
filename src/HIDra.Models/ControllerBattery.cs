namespace HIDra.Models;

/// <summary>
/// How the controller is powered. A wired controller cannot run out of charge,
/// so battery warnings are suppressed for it.
/// </summary>
public enum BatteryPowerType
{
    /// <summary>No battery information is available (usually means no controller).</summary>
    Unknown,

    /// <summary>Connected by cable - cannot go flat mid-session.</summary>
    Wired,

    /// <summary>Disposable alkaline batteries.</summary>
    Alkaline,

    /// <summary>Rechargeable battery pack.</summary>
    Rechargeable
}

/// <summary>
/// Remaining charge, as reported by XInput. XInput only reports four coarse
/// levels - there is no percentage available.
/// </summary>
public enum BatteryChargeLevel
{
    Unknown,
    Empty,
    Low,
    Medium,
    Full
}

/// <summary>
/// Battery state of a connected controller.
///
/// This exists so a member of staff can see, at the start of a session, whether the
/// controller will last - and so the student gets a warning well before it dies,
/// rather than discovering it when the controller stops responding and they have no
/// other way to use the machine.
/// </summary>
public class ControllerBattery
{
    public BatteryPowerType PowerType { get; set; } = BatteryPowerType.Unknown;

    public BatteryChargeLevel ChargeLevel { get; set; } = BatteryChargeLevel.Unknown;

    /// <summary>
    /// True when the controller runs on batteries that are low or empty, and so
    /// needs attention before it strands the user.
    /// </summary>
    public bool NeedsAttention =>
        (PowerType == BatteryPowerType.Alkaline || PowerType == BatteryPowerType.Rechargeable)
        && (ChargeLevel == BatteryChargeLevel.Low || ChargeLevel == BatteryChargeLevel.Empty);

    /// <summary>
    /// Short human-readable summary for the status window, aimed at staff rather
    /// than at a developer.
    /// </summary>
    public string Description => PowerType switch
    {
        BatteryPowerType.Wired => "Wired (no battery)",
        BatteryPowerType.Unknown => "Battery unknown",
        _ => ChargeLevel switch
        {
            BatteryChargeLevel.Full => "Battery full",
            BatteryChargeLevel.Medium => "Battery OK",
            BatteryChargeLevel.Low => "Battery LOW - change or charge soon",
            BatteryChargeLevel.Empty => "Battery EMPTY - change now",
            _ => "Battery unknown"
        }
    };

    public ControllerBattery Clone() => new()
    {
        PowerType = PowerType,
        ChargeLevel = ChargeLevel
    };

    public bool SameAs(ControllerBattery? other) =>
        other != null && other.PowerType == PowerType && other.ChargeLevel == ChargeLevel;
}
