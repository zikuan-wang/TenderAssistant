using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Win32;
using TenderAssistant.Licensing;

namespace TenderAssistant.Authorizer;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private OfflineLicenseRequest? _request;
    private string _requestSummary = "尚未导入申请文件。";
    private string _statusMessage = "等待导入授权申请。";
    private DateTime? _expiresAt = DateTime.Today.AddYears(1);
    private string _edition = "Standard";

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string RequestSummary
    {
        get => _requestSummary;
        private set => SetProperty(ref _requestSummary, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public DateTime? ExpiresAt
    {
        get => _expiresAt;
        set => SetProperty(ref _expiresAt, value);
    }

    public string Edition
    {
        get => _edition;
        set => SetProperty(ref _edition, value);
    }

    private void OnImportRequestClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入授权申请文件",
            Filter = "授权申请 JSON (*.json)|*.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _request = OfflineLicenseJson.DeserializeRequest(File.ReadAllText(dialog.FileName))
            ?? throw new InvalidOperationException("授权申请文件格式无效。");
        RequestSummary = $"申请人：{_request.ApplicantName}\r\n设备：{_request.MachineName}\r\n设备指纹：{_request.DeviceFingerprint}\r\n申请时间：{_request.CreatedAtUtc.LocalDateTime:yyyy-MM-dd HH:mm:ss}\r\n申请编号：{_request.RequestId}";
        StatusMessage = "申请文件已导入。";
    }

    private void OnGenerateLicenseClick(object sender, RoutedEventArgs e)
    {
        if (_request is null)
        {
            StatusMessage = "请先导入授权申请文件。";
            return;
        }

        if (ExpiresAt is null)
        {
            StatusMessage = "请选择授权到期时间。";
            return;
        }

        var payload = new LicensePayload(
            Guid.NewGuid().ToString("N"),
            _request.ApplicantName,
            _request.DeviceFingerprint,
            ["BidAssist"],
            DateTimeOffset.UtcNow,
            new DateTimeOffset(ExpiresAt.Value.Date.AddDays(1).AddTicks(-1)),
            string.IsNullOrWhiteSpace(Edition) ? "Standard" : Edition.Trim());

        var envelope = OfflineLicenseCrypto.Sign(payload);
        var dialog = new SaveFileDialog
        {
            FileName = $"TenderAssistant-Activation-{_request.MachineName}-{DateTime.Now:yyyyMMddHHmmss}.bali",
            Filter = "激活文件 (*.bali)|*.bali",
            Title = "生成激活文件"
        };

        if (dialog.ShowDialog() == true)
        {
            File.WriteAllText(dialog.FileName, OfflineLicenseCrypto.EncryptEnvelope(envelope));
            StatusMessage = $"激活文件已生成：{dialog.FileName}";
        }
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
