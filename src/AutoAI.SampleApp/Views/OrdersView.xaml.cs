using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AutoAI.SampleApp.Data;
using AutoAI.SampleApp.Models;

namespace AutoAI.SampleApp.Views;

public partial class OrdersView : UserControl
{
    private readonly ObservableCollection<Order> _orders = [];
    private readonly ICollectionView _ordersView;

    public OrdersView()
    {
        InitializeComponent();

        _ordersView = CollectionViewSource.GetDefaultView(_orders);
        _ordersView.Filter = FilterOrder;
        OrdersGrid.ItemsSource = _ordersView;

        OrdersFilterComboBox.ItemsSource = SampleData.OrderStatuses;
        OrdersFilterComboBox.SelectedIndex = 0;
    }

    private async void OrdersLoadButton_Click(object sender, RoutedEventArgs e)
    {
        OrdersLoadButton.IsEnabled = false;
        OrdersStatusText.Text = "Loading...";
        _orders.Clear();
        CompletedOrdersGrid.ItemsSource = null;

        // Simulated backend latency so the agent has to wait for the data to load.
        await Task.Delay(1500);

        foreach (var order in SampleData.Orders)
        {
            _orders.Add(order);
        }
        CompletedOrdersGrid.ItemsSource = SampleData.Orders.Where(o => o.Status == "Shipped").ToList();
        OrdersStatusText.Text = $"Loaded {_orders.Count} orders";
        OrdersLoadButton.IsEnabled = true;
    }

    private void OrdersClearButton_Click(object sender, RoutedEventArgs e)
    {
        _orders.Clear();
        CompletedOrdersGrid.ItemsSource = null;
        OrdersStatusText.Text = "Not loaded";
    }

    private void OrdersDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (OrdersGrid.SelectedItem is not Order selected)
        {
            OrdersStatusText.Text = "Select an order to delete";
            return;
        }

        var answer = MessageBox.Show(Window.GetWindow(this)!,
            $"Delete order {selected.OrderId} ({selected.Customer})?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes)
        {
            _orders.Remove(selected);
            OrdersStatusText.Text = $"Deleted order {selected.OrderId} - {_orders.Count} orders remaining";
        }
        else
        {
            OrdersStatusText.Text = "Delete cancelled";
        }
    }

    private void OrdersFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ordersView?.Refresh();
    }

    private bool FilterOrder(object item)
    {
        var filter = OrdersFilterComboBox.SelectedItem as string;
        return filter is null or "All" || (item is Order order && order.Status == filter);
    }
}
