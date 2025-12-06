using System;
using System.Windows;
using HIDra.Models;

namespace HIDra.UI.Views;

public partial class SettingsWindow : Window
{
    private readonly InputSettings _settings;
    private const float MinValue = 0.1f;
    private const float MaxValue = 1.0f;
    private const float Increment = 0.05f;

    public SettingsWindow(InputSettings settings)
    {
        InitializeComponent();
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        // Set initial values from current settings
        UpdateValueDisplay();
        
        // Set checkbox state from settings (defaults to true if not set)
        if (Grid3AutoSuspendCheckBox != null)
        {
            // Temporarily unhook events to avoid triggering during initialization
            Grid3AutoSuspendCheckBox.Checked -= Grid3AutoSuspendCheckBox_Changed;
            Grid3AutoSuspendCheckBox.Unchecked -= Grid3AutoSuspendCheckBox_Changed;
            
            Grid3AutoSuspendCheckBox.IsChecked = _settings.EnableGrid3AutoSuspend;
            
            // Re-hook events after setting initial value
            Grid3AutoSuspendCheckBox.Checked += Grid3AutoSuspendCheckBox_Changed;
            Grid3AutoSuspendCheckBox.Unchecked += Grid3AutoSuspendCheckBox_Changed;
        }
    }

    private void UpButton_Click(object sender, RoutedEventArgs e)
    {
        // Increase sensitivity by 0.5
        float newValue = _settings.CursorSensitivity + Increment;
        if (newValue <= MaxValue)
        {
            _settings.CursorSensitivity = newValue;
            UpdateValueDisplay();
        }
    }

    private void DownButton_Click(object sender, RoutedEventArgs e)
    {
        // Decrease sensitivity by 0.5
        float newValue = _settings.CursorSensitivity - Increment;
        if (newValue >= MinValue)
        {
            _settings.CursorSensitivity = newValue;
            UpdateValueDisplay();
        }
    }

    private void UpdateValueDisplay()
    {
        if (SensitivityValueText != null)
        {
            SensitivityValueText.Text = $"{_settings.CursorSensitivity:F2}x";
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Grid3AutoSuspendCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // Guard against event firing during InitializeComponent before _settings is assigned
        if (_settings == null) return;
        
        _settings.EnableGrid3AutoSuspend = Grid3AutoSuspendCheckBox.IsChecked == true;
    }
}
