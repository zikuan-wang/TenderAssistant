using System.Windows.Controls;

namespace TenderAssistant.Client.Views.Pages;

public partial class AboutPageView : UserControl
{
    public AboutPageView()
    {
        InitializeComponent();
        DataContext = this;
    }

    public string VersionText => $"版本：{GetType().Assembly.GetName().Version?.ToString(3) ?? "1.0.1"}";
}
