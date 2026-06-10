using System.Windows;
using System.Windows.Controls;

namespace AutoAI.Copilot.Behaviors;

/// <summary>Keeps a ScrollViewer pinned to the bottom while new chat items are added.</summary>
public static class AutoScrollBehavior
{
    public static readonly DependencyProperty AutoScrollProperty =
        DependencyProperty.RegisterAttached(
            "AutoScroll",
            typeof(bool),
            typeof(AutoScrollBehavior),
            new PropertyMetadata(false, OnAutoScrollChanged));

    public static bool GetAutoScroll(DependencyObject obj) => (bool)obj.GetValue(AutoScrollProperty);

    public static void SetAutoScroll(DependencyObject obj, bool value) => obj.SetValue(AutoScrollProperty, value);

    private static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer scrollViewer)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            scrollViewer.ScrollChanged += OnScrollChanged;
        }
        else
        {
            scrollViewer.ScrollChanged -= OnScrollChanged;
        }
    }

    private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // Content grew (new message) -> follow the bottom.
        if (e.ExtentHeightChange > 0)
        {
            ((ScrollViewer)sender).ScrollToEnd();
        }
    }
}
