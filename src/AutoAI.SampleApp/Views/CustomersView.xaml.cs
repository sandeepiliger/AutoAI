using System.Windows;
using System.Windows.Controls;
using AutoAI.SampleApp.Data;
using AutoAI.SampleApp.Models;

namespace AutoAI.SampleApp.Views;

public partial class CustomersView : UserControl
{
    public CustomersView()
    {
        InitializeComponent();
    }

    private void CustomersView_Loaded(object sender, RoutedEventArgs e)
    {
        if (CustomersGrid.ItemsSource is null)
        {
            ShowCustomers(SampleData.Customers);
        }
    }

    private void CustomerSearchButton_Click(object sender, RoutedEventArgs e)
    {
        var query = CustomerSearchTextBox.Text.Trim();
        var matches = string.IsNullOrEmpty(query)
            ? SampleData.Customers
            : SampleData.Customers
                .Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        ShowCustomers(matches);
    }

    private void ShowCustomers(IReadOnlyList<Customer> customers)
    {
        CustomersGrid.ItemsSource = customers;
        CustomersCountText.Text = $"{customers.Count} customers shown";
    }
}
