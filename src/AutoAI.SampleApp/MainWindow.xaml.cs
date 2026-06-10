using System.Windows;
using AutoAI.SampleApp.Dialogs;
using AutoAI.SampleApp.Views;

namespace AutoAI.SampleApp;

public partial class MainWindow : Window
{
    private readonly HomeView _homeView = new();
    private readonly OrdersView _ordersView = new();
    private readonly CustomersView _customersView = new();
    private readonly OrderEntryView _orderEntryView = new();
    private readonly ReportsView _reportsView = new();

    public MainWindow()
    {
        InitializeComponent();
        Navigate(_homeView, "Home");
    }

    private void Navigate(object view, string screenName)
    {
        ContentHost.Content = view;
        StatusBarText.Text = $"Ready - {screenName}";
    }

    private void NavHomeButton_Click(object sender, RoutedEventArgs e) => Navigate(_homeView, "Home");
    private void NavOrdersButton_Click(object sender, RoutedEventArgs e) => Navigate(_ordersView, "Orders");
    private void NavCustomersButton_Click(object sender, RoutedEventArgs e) => Navigate(_customersView, "Customers");
    private void NavOrderEntryButton_Click(object sender, RoutedEventArgs e) => Navigate(_orderEntryView, "Order Entry");
    private void NavReportsButton_Click(object sender, RoutedEventArgs e) => Navigate(_reportsView, "Reports");

    private void ViewHomeMenuItem_Click(object sender, RoutedEventArgs e) => Navigate(_homeView, "Home");
    private void ViewOrdersMenuItem_Click(object sender, RoutedEventArgs e) => Navigate(_ordersView, "Orders");
    private void ViewCustomersMenuItem_Click(object sender, RoutedEventArgs e) => Navigate(_customersView, "Customers");
    private void ViewOrderEntryMenuItem_Click(object sender, RoutedEventArgs e) => Navigate(_orderEntryView, "Order Entry");
    private void ViewReportsMenuItem_Click(object sender, RoutedEventArgs e) => Navigate(_reportsView, "Reports");

    private void FileNewOrderMenuItem_Click(object sender, RoutedEventArgs e) => Navigate(_orderEntryView, "Order Entry");

    private void FileExitMenuItem_Click(object sender, RoutedEventArgs e) => Close();

    private void ToolsSettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog { Owner = this };
        var saved = dialog.ShowDialog() == true;
        StatusBarText.Text = saved
            ? $"Settings saved - Theme: {dialog.SelectedTheme}, Auto refresh: {dialog.AutoRefreshEnabled}"
            : "Settings cancelled";
    }

    private void HelpAboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this,
            "AutoAI Sample App\nVersion 1.0\n\nAn enterprise-style WPF application used as the target for AI-driven UI automation tests.",
            "About AutoAI Sample App",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
