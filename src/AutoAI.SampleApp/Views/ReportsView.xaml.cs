using System.Windows;
using System.Windows.Controls;

namespace AutoAI.SampleApp.Views;

public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
    }

    private void ReportsTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        ReportsSelectedText.Text = e.NewValue is TreeViewItem item
            ? $"Selected: {item.Header}"
            : "No report selected";
    }

    private async void ReportsGenerateButton_Click(object sender, RoutedEventArgs e)
    {
        if (ReportsTreeView.SelectedItem is not TreeViewItem selected || selected.HasItems)
        {
            ReportsStatusText.Text = "Select a report (a leaf item) first";
            return;
        }

        ReportsGenerateButton.IsEnabled = false;
        ReportsStatusText.Text = "Generating...";
        ReportsResultList.Items.Clear();
        ReportsProgressBar.Value = 0;

        // Simulated long-running report so the agent can observe progress and wait.
        for (var progress = 10; progress <= 100; progress += 10)
        {
            await Task.Delay(200);
            ReportsProgressBar.Value = progress;
        }

        var report = (string)selected.Header;
        var detailed = ReportsDetailedCheckBox.IsChecked == true;
        var rows = detailed ? 8 : 5;
        for (var i = 1; i <= rows; i++)
        {
            ReportsResultList.Items.Add($"{report} - line {i}: value {i * 1250:N0}");
        }
        if (ReportsIncludeChartsCheckBox.IsChecked == true)
        {
            ReportsResultList.Items.Add($"{report} - [chart attached]");
        }

        ReportsStatusText.Text = $"Report ready ({ReportsResultList.Items.Count} lines)";
        ReportsGenerateButton.IsEnabled = true;
    }
}
