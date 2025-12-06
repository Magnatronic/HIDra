using HIDra.Models;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace HIDra.Core.Configuration;

/// <summary>
/// Manages loading and saving of controller mapping configurations
/// </summary>
public class ConfigurationService
{
    private const string DefaultConfigFileName = "default-mappings.json";
    private const string UserConfigFileName = "user-mappings.json";
    private readonly string _configDirectory;

    public ConfigurationService(string? configDirectory = null)
    {
        // Use provided directory or default to config folder next to executable
        _configDirectory = configDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");
        
        // Ensure config directory exists
        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
        }
    }

    /// <summary>
    /// Load configuration (tries user config first, falls back to default)
    /// </summary>
    public async Task<MappingConfiguration> LoadConfigurationAsync()
    {
        // Try to load user configuration first
        var userConfigPath = Path.Combine(_configDirectory, UserConfigFileName);
        if (File.Exists(userConfigPath))
        {
            try
            {
                var userConfig = await LoadConfigFromFileAsync(userConfigPath);
                if (userConfig != null)
                {
                    return userConfig;
                }
            }
            catch (Exception ex)
            {
                // Log error but continue to default config
                Console.WriteLine($"Error loading user config: {ex.Message}");
            }
        }

        // Fall back to default configuration
        var defaultConfigPath = Path.Combine(_configDirectory, DefaultConfigFileName);
        if (File.Exists(defaultConfigPath))
        {
            var defaultConfig = await LoadConfigFromFileAsync(defaultConfigPath);
            if (defaultConfig != null)
            {
                return defaultConfig;
            }
        }

        // If no config files exist, return default configuration
        return CreateDefaultConfiguration();
    }

    /// <summary>
    /// Load configuration from specific file
    /// </summary>
    private async Task<MappingConfiguration?> LoadConfigFromFileAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var config = JsonConvert.DeserializeObject<MappingConfiguration>(json);
            return config;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load configuration from {filePath}", ex);
        }
    }

    /// <summary>
    /// Save user configuration
    /// </summary>
    public async Task SaveUserConfigurationAsync(MappingConfiguration config)
    {
        try
        {
            var userConfigPath = Path.Combine(_configDirectory, UserConfigFileName);
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            await File.WriteAllTextAsync(userConfigPath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to save user configuration", ex);
        }
    }

    /// <summary>
    /// Apply configuration settings to InputSettings
    /// </summary>
    public InputSettings ApplyConfigurationSettings(MappingConfiguration config)
    {
        return new InputSettings
        {
            CursorSensitivity = config.Settings.CursorSensitivity,
            ScrollSensitivity = config.Settings.ScrollSensitivity,
            PrecisionModeSensitivity = config.Settings.PrecisionModeSensitivity,
            Deadzone = config.Settings.Deadzone,
            PollRateMs = config.Settings.PollRateMs,
            StickCalibrationMax = config.Settings.StickCalibrationMax,
            TriggerThreshold = config.Settings.TriggerThreshold
        };
    }

    /// <summary>
    /// Create default configuration if no files exist
    /// </summary>
    private MappingConfiguration CreateDefaultConfiguration()
    {
        return new MappingConfiguration
        {
            Profile = new ProfileInfo
            {
                Name = "Default Xbox Controller",
                Version = "1.0",
                ControllerType = "Xbox",
                Description = "Default mapping optimized for Windows UI navigation"
            },
            Settings = new MappingSettings
            {
                CursorSensitivity = 0.5f, // Slower for accessibility
                ScrollSensitivity = 0.5f, // Optimized for accessibility
                PrecisionModeSensitivity = 0.3f,
                Deadzone = 0.05f, // Low deadzone for maximum control
                PollRateMs = 10,
                StickCalibrationMax = 0.90f, // For worn controllers
                TriggerThreshold = 0.3f
            },
            Buttons = new System.Collections.Generic.Dictionary<string, ButtonMapping>
            {
                ["ButtonA"] = new ButtonMapping 
                { 
                    Default = new ActionMapping { Action = "MouseLeftClick", Description = "Left mouse click" }
                },
                ["ButtonB"] = new ButtonMapping 
                { 
                    Default = new ActionMapping { Action = "MouseRightClick", Description = "Right mouse click" }
                },
                ["ButtonX"] = new ButtonMapping 
                { 
                    Default = new ActionMapping { Action = "ToggleOnScreenKeyboard", Description = "Toggle virtual keyboard" }
                },
                ["ButtonY"] = new ButtonMapping 
                { 
                    Default = new ActionMapping { Action = "SwapStickModes", Description = "Swap left/right stick modes" }
                },
                ["LeftBumper"] = new ButtonMapping 
                { 
                    Default = new ActionMapping { Action = "TaskSwitcherBackward", Description = "Switch to previous app" }
                },
                ["RightBumper"] = new ButtonMapping 
                { 
                    Default = new ActionMapping { Action = "MouseDoubleClick", Description = "Double click" }
                },
                ["Back"] = new ButtonMapping 
                { 
                    Default = new ActionMapping { Action = "WindowsKey", Description = "Open Start Menu" }
                },
                ["Start"] = new ButtonMapping 
                { 
                    Default = new ActionMapping { Action = "WindowsTab", Description = "Task View" }
                },
                ["DpadUp"] = new ButtonMapping 
                { 
                    Default = new ActionMapping { Action = "MaximizeWindow", Description = "Maximize window" }
                },
                ["DpadDown"] = new ButtonMapping 
                { 
                    Default = new ActionMapping { Action = "MinimizeWindow", Description = "Minimize window" }
                },
                ["DpadLeft"] = new ButtonMapping 
                { 
                    Default = new ActionMapping 
                    { 
                        Action = "KeyCombo", 
                        Keys = new System.Collections.Generic.List<string> { "LWin", "Left" },
                        Description = "Snap window left" 
                    }
                },
                ["DpadRight"] = new ButtonMapping 
                { 
                    Default = new ActionMapping 
                    { 
                        Action = "KeyCombo", 
                        Keys = new System.Collections.Generic.List<string> { "LWin", "Right" },
                        Description = "Snap window right" 
                    }
                }
            }
        };
    }

    /// <summary>
    /// Reset to default configuration
    /// </summary>
    public async Task ResetToDefaultAsync()
    {
        var userConfigPath = Path.Combine(_configDirectory, UserConfigFileName);
        if (File.Exists(userConfigPath))
        {
            File.Delete(userConfigPath);
        }
    }

    /// <summary>
    /// Export current configuration to a file
    /// </summary>
    public async Task ExportConfigurationAsync(MappingConfiguration config, string filePath)
    {
        try
        {
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to export configuration to {filePath}", ex);
        }
    }

    /// <summary>
    /// Import configuration from a file
    /// </summary>
    public async Task<MappingConfiguration> ImportConfigurationAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Configuration file not found: {filePath}");
        }

        var config = await LoadConfigFromFileAsync(filePath);
        if (config == null)
        {
            throw new InvalidOperationException("Failed to import configuration");
        }

        return config;
    }
}
