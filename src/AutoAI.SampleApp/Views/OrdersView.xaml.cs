using System.Windows;
using System.Windows.Controls;
using AutoAI.SampleApp.Data;

namespace AutoAI.SampleApp.Views;

public partial class OrdersView : UserControl
{
    public OrdersView()
    {
        InitializeComponent();
    }

    private async void OrdersLoadButton_Click(object sender, RoutedEventArgs e)
    {
        OrdersLoadButton.IsEnabled = false;
        OrdersStatusText.Text = "Loading...";
        OrdersGrid.ItemsSource = null;

        // Simulated backend latency so the agent has to wait for the data to load.
        await Task.Delay(1500);

        OrdersGrid.ItemsSource = SampleData.Orders;
        OrdersStatusText.Text = $"Loaded {SampleData.Orders.Count} orders";
        OrdersLoadButton.IsEnabled = true;
    }

    private void OrdersClearButton_Click(object sender, RoutedEventArgs e)
    {
        OrdersGrid.ItemsSource = null;
        OrdersStatusText.Text = "Not loaded";
    }
}
