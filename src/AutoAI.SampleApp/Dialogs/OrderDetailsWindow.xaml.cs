using System.Windows;
using AutoAI.SampleApp.Models;

namespace AutoAI.SampleApp.Dialogs;

public partial class OrderDetailsWindow : Window
{
    public OrderDetailsWindow(Order order)
    {
        InitializeComponent();
        Title = $"Order Details - {order.OrderId}";
        OrderDetailIdText.Text = $"Order Id: {order.OrderId}";
        OrderDetailCustomerText.Text = $"Customer: {order.Customer}";
        OrderDetailAmountText.Text = $"Amount: {order.Amount:N2}";
        OrderDetailStatusText.Text = $"Status: {order.Status}";
    }

    private void OrderDetailCloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
