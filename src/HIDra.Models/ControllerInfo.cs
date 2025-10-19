namespace HIDra.Models;

/// <summary>
/// Type of controller detected
/// </summary>
public enum ControllerType
{
    Unknown,
    Xbox360,
    XboxOne,
    XboxSeriesX,
    PlayStation4,
    PlayStation5,
    SwitchPro,
    Generic
}

/// <summary>
/// Connection status of the controller
/// </summary>
public enum ConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

/// <summary>
/// Information about a connected controller
/// </summary>
public class ControllerInfo
{
    /// <summary>
    /// Unique identifier for this controller
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Type of controller
    /// </summary>
    public ControllerType Type { get; set; }

    /// <summary>
    /// Friendly name for display
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// USB Vendor ID
    /// </summary>
    public int VendorId { get; set; }

    /// <summary>
    /// USB Product ID
    /// </summary>
    public int ProductId { get; set; }

    /// <summary>
    /// Current connection status
    /// </summary>
    public ConnectionStatus Status { get; set; }

    /// <summary>
    /// Is the controller currently connected and ready
    /// </summary>
    public bool IsConnected => Status == ConnectionStatus.Connected;
}
