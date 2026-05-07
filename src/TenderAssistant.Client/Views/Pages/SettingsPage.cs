using System.Windows.Controls;
using TenderAssistant.Client.ViewModels.Pages;

namespace TenderAssistant.Client.Views.Pages;

public sealed class SettingsPage : Page
{
    public SettingsPage()
    {
        Content = new SettingsPageView();
        DataContext = new SettingsPageViewModel();
    }
}
