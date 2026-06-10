using System.Windows;
using System.Windows.Controls;

namespace AutoAI.SampleApp.Dialogs;

public partial class SettingsDialog : Window
{
    public SettingsDialog()
    {
        InitializeComponent();
    }

    public string SelectedTheme =>
        (SettingsThemeComboBox.SelectedItem as ComboBoxItem)?.Content as string ?? "Light";

    public bool AutoRefreshEnabled => SettingsAutoRefreshCheckBox.IsChecked == true;

    private void SettingsSaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
