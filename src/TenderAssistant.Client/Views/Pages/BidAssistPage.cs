using System.Windows.Controls;
using TenderAssistant.Client.ViewModels.Pages;

namespace TenderAssistant.Client.Views.Pages;

public sealed class BidAssistPage : Page
{
    public BidAssistPage()
    {
        Content = new BidAssistPageView();
        DataContext = new BidAssistPageViewModel();
    }
}
