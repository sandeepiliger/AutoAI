using System.Windows;
using System.Windows.Controls;
using AutoAI.SampleApp.Data;

namespace AutoAI.SampleApp.Views;

public partial class OrderEntryView : UserControl
{
    private static int _nextOrderId = 1011;

    public OrderEntryView()
    {
        InitializeComponent();
        OrderEntryCustomerComboBox.ItemsSource = SampleData.Customers.Select(c => c.Name).ToList();
        OrderEntryProductComboBox.ItemsSource = SampleData.Products;
    }

    private void OrderEntrySubmitButton_Click(object sender, RoutedEventArgs e)
    {
        var errors = new List<string>();
        if (OrderEntryCustomerComboBox.SelectedItem is null)
        {
            errors.Add("select a customer");
        }
        if (OrderEntryProductComboBox.SelectedItem is null)
        {
            errors.Add("select a product");
        }
        if (!int.TryParse(OrderEntryQuantityTextBox.Text, out var quantity) || quantity < 1)
        {
            errors.Add("enter a quantity of 1 or more");
        }

        if (errors.Count > 0)
        {
            OrderEntryStatusText.Text = $"Validation failed: {string.Join(", ", errors)}.";
            return;
        }

        var priority = OrderEntryPriorityHighRadio.IsChecked == true ? "High"
            : OrderEntryPriorityLowRadio.IsChecked == true ? "Low"
            : "Normal";
        var extras = new List<string>();
        if (OrderEntryExpressCheckBox.IsChecked == true) extras.Add("express shipping");
        if (OrderEntryGiftWrapCheckBox.IsChecked == true) extras.Add("gift wrap");
        var delivery = OrderEntryDatePicker.SelectedDate?.ToString("yyyy-MM-dd") ?? "unscheduled";

        OrderEntryStatusText.Text =
            $"Order ORD-{_nextOrderId++} created: {OrderEntryQuantityTextBox.Text} x " +
            $"{OrderEntryProductComboBox.SelectedItem} for {OrderEntryCustomerComboBox.SelectedItem}, " +
            $"{priority} priority, {(int)OrderEntryDiscountSlider.Value}% discount, delivery {delivery}" +
            (extras.Count > 0 ? $", with {string.Join(" and ", extras)}." : ".");
    }

    private void OrderEntryResetButton_Click(object sender, RoutedEventArgs e)
    {
        OrderEntryCustomerComboBox.SelectedItem = null;
        OrderEntryProductComboBox.SelectedItem = null;
        OrderEntryQuantityTextBox.Text = "1";
        OrderEntryDatePicker.SelectedDate = null;
        OrderEntryPriorityNormalRadio.IsChecked = true;
        OrderEntryExpressCheckBox.IsChecked = false;
        OrderEntryGiftWrapCheckBox.IsChecked = false;
        OrderEntryDiscountSlider.Value = 0;
        OrderEntryNotesTextBox.Text = string.Empty;
        OrderEntryStatusText.Text = "Form reset";
    }

    private void OrderEntryDiscountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OrderEntryDiscountText is not null)
        {
            OrderEntryDiscountText.Text = $"{(int)e.NewValue}%";
        }
    }
}
