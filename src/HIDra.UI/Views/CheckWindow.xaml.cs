using System.Windows;

namespace HIDra.UI.Views
{
    /// <summary>
    /// Shows the "Check this PC" report. The report is run before the window is shown,
    /// since it takes a moment when a network folder is slow to answer.
    /// </summary>
    public partial class CheckWindow : Window
    {
        public CheckWindow(string report)
        {
            InitializeComponent();
            ReportText.Text = report;
            SavedText.Text = $"Also saved as {PcCheck.Location}";
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(ReportText.Text);
                SavedText.Text = "Copied - paste it into an email or message";
            }
            catch
            {
                // The clipboard can be busy; the text can still be selected and copied
                SavedText.Text = "Could not copy just now - try again, or select the text and press Ctrl+C";
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
