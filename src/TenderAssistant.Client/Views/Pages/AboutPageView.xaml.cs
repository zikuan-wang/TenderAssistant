using System.Windows.Controls;
using TenderAssistant.Client.Services;

namespace TenderAssistant.Client.Views.Pages;

public partial class AboutPageView : UserControl
{
    public AboutPageView()
    {
        InitializeComponent();
        DataContext = this;
    }

    public string VersionText => $"版本：{AppVersionService.CurrentVersionText}";
}
