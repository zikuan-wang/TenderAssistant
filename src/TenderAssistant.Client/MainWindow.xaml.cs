using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TenderAssistant.Client.Infrastructure;
using TenderAssistant.Client.Services;
using TenderAssistant.Client.ViewModels;

namespace TenderAssistant.Client;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        OperationLogService.Info("app", "startup", "客户端启动。");

        InitializeComponent();
        DataContext = new MainWindowViewModel();
        Loaded += OnLoaded;
        AddHandler(UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnGlobalPreviewMouseWheel), true);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BidAssistFrame.Navigate(new Views.Pages.BidAssistPage());
        SettingsFrame.Navigate(new Views.Pages.SettingsPage());
        AboutFrame.Navigate(new Views.Pages.AboutPage());
    }

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        MainTabs.SelectedItem = AboutTab;
    }

    private void OnGlobalPreviewMouseWheel(object sender, MouseWheelEventArgs args)
    {
        if (args.OriginalSource is DependencyObject source)
        {
            var viewer = FindAncestorScrollViewer(source);
            if (viewer is not null && MouseWheelScroll.TryScroll(viewer, args.Delta))
            {
                args.Handled = true;
            }
        }
    }

    private static ScrollViewer? FindAncestorScrollViewer(DependencyObject source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is ScrollViewer viewer && viewer.IsVisible && viewer.ScrollableHeight > 0)
            {
                return viewer;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
