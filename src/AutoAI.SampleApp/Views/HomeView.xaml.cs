using System.Windows;
using System.Windows.Controls;

namespace AutoAI.SampleApp.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    private void HomeGreetButton_Click(object sender, RoutedEventArgs e)
    {
        var name = HomeUserNameTextBox.Text.Trim();
        HomeGreetingText.Text = string.IsNullOrEmpty(name)
            ? "Hello, stranger!"
            : $"Hello, {name}!";
    }
}
