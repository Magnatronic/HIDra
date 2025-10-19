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
        _settings = settings;

        // Set initial value from current settings
        UpdateValueDisplay();
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
}
