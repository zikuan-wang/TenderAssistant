using TenderAssistant.Client.Infrastructure;
using TenderAssistant.Client.Services;

namespace TenderAssistant.Client.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly OfflineLicenseService _licenseService = new();

    public MainWindowViewModel()
    {
        OfflineLicenseService.LicenseChanged += (_, _) => OnPropertyChanged(nameof(LicenseStatus));
    }

    public TaskStatusService TaskStatus => TaskStatusService.Instance;

    public string CurrentUser => Environment.UserName;

    public string LicenseStatus
    {
        get
        {
            var result = _licenseService.ValidateCurrent();
            return result.IsValid ? "已激活" : "未激活";
        }
    }
}
