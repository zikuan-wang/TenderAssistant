using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TenderAssistant.Client.Infrastructure;

public static class MouseWheelScroll
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(MouseWheelScroll),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element)
    {
        return (bool)element.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not ScrollViewer viewer)
        {
            return;
        }

        viewer.PreviewMouseWheel -= OnPreviewMouseWheel;
        if ((bool)args.NewValue)
        {
            viewer.PreviewMouseWheel += OnPreviewMouseWheel;
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs args)
    {
        if (sender is not ScrollViewer viewer)
        {
            return;
        }

        args.Handled = TryScroll(viewer, args.Delta);
    }

    public static bool TryScroll(ScrollViewer viewer, int delta)
    {
        if (viewer.ScrollableHeight <= 0)
        {
            return false;
        }

        var targetOffset = Math.Clamp(viewer.VerticalOffset - delta, 0, viewer.ScrollableHeight);
        if (Math.Abs(targetOffset - viewer.VerticalOffset) < 0.1)
        {
            return false;
        }

        viewer.ScrollToVerticalOffset(targetOffset);
        return true;
    }
}
