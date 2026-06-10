using System.Windows;

namespace AutoAI.Copilot;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => InputBox.Focus();
    }

    private void TitleBarMinimizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void TitleBarMaximizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void TitleBarCloseButton_Click(object sender, RoutedEventArgs e)
        => Close();
}
