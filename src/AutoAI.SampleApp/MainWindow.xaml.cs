using System.Windows;
using AutoAI.SampleApp.Views;

namespace AutoAI.SampleApp;

public partial class MainWindow : Window
{
    private readonly HomeView _homeView = new();
    private readonly OrdersView _ordersView = new();
    private readonly CustomersView _customersView = new();

    public MainWindow()
    {
        InitializeComponent();
        ContentHost.Content = _homeView;
    }

    private void NavHomeButton_Click(object sender, RoutedEventArgs e)
        => ContentHost.Content = _homeView;

    private void NavOrdersButton_Click(object sender, RoutedEventArgs e)
        => ContentHost.Content = _ordersView;

    private void NavCustomersButton_Click(object sender, RoutedEventArgs e)
        => ContentHost.Content = _customersView;
}
