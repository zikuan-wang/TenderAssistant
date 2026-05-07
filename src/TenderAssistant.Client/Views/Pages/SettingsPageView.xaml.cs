using System.Windows.Controls;
using TenderAssistant.Client.ViewModels.Pages;

namespace TenderAssistant.Client.Views.Pages;

public partial class SettingsPageView : UserControl
{
    public SettingsPageView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is SettingsPageViewModel viewModel)
            {
                viewModel.Refresh();
            }
        };
    }
}
