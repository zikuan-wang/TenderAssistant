using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Win32;
using TenderAssistant.Client.Infrastructure;
using TenderAssistant.Client.Services;

namespace TenderAssistant.Client.ViewModels.Pages;

public sealed class SettingsPageViewModel : ObservableObject
{
    private readonly OfflineLicenseService _licenseService = new();
    private readonly BidAssistFileCatalogService _fileCatalogService = new();
    private readonly GitHubUpdateService _updateService = new();
    private string _statusMessage = "日志系统已启用，当前保留最近操作，超过 2000 条自动归档。";
    private ClientApplicationTheme _selectedTheme = AppThemeService.CurrentTheme;
    private string _fileLibraryCachePath = ClientAppSettingsService.FileLibraryCachePath;
    private string _sourceFileLibraryPath = ClientAppSettingsService.SourceFileLibraryPath;
    private string _applicantName = Environment.UserName;
    private string _licenseStatus = string.Empty;
    private string _updateStatusMessage = "尚未检查更新。";
    private UpdateCheckResult? _latestUpdate;

    public SettingsPageViewModel()
    {
        RefreshCommand = new RelayCommand(_ => Refresh());
        ArchiveCommand = new RelayCommand(_ => Archive());
        ClearCommand = new RelayCommand(_ => Clear());
        DownloadCommand = new RelayCommand(_ => Download());
        BrowseFileLibraryCacheCommand = new RelayCommand(_ => BrowseFileLibraryCachePath());
        BrowseSourceFileLibraryCommand = new RelayCommand(_ => BrowseSourceFileLibraryPath());
        SyncFileLibraryCommand = new AsyncRelayCommand(SyncFileLibraryAsync);
        ExportLicenseRequestCommand = new RelayCommand(_ => ExportLicenseRequest());
        ImportActivationCommand = new RelayCommand(_ => ImportActivation());
        CheckUpdateCommand = new AsyncRelayCommand(CheckUpdateAsync);
        InstallUpdateCommand = new AsyncRelayCommand(InstallUpdateAsync, () => CanInstallUpdate);
        OpenReleasePageCommand = new RelayCommand(_ => OpenReleasePage());

        OperationLogService.Info("settings", "open", "打开设置与日志页面。");
        Refresh();
    }

    public ObservableCollection<OperationLogEntry> Logs { get; } = new();

    public ObservableCollection<LogArchiveItem> Archives { get; } = new();

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new("浅色", ClientApplicationTheme.Light),
        new("深色", ClientApplicationTheme.Dark),
        new("粉色", ClientApplicationTheme.Pink)
    ];

    public ClientApplicationTheme SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (!SetProperty(ref _selectedTheme, value))
            {
                return;
            }

            AppThemeService.SetTheme(value);
            OperationLogService.Info("settings", "change-theme", $"应用主题切换为：{value}。");
            Refresh();
            StatusMessage = $"应用主题已切换为：{value}。";
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string FileLibraryCachePath
    {
        get => _fileLibraryCachePath;
        set
        {
            if (!SetProperty(ref _fileLibraryCachePath, value))
            {
                return;
            }

            ClientAppSettingsService.FileLibraryCachePath = value;
            StatusMessage = "本地文件库位置已保存。";
        }
    }

    public string SourceFileLibraryPath
    {
        get => _sourceFileLibraryPath;
        set
        {
            if (!SetProperty(ref _sourceFileLibraryPath, value))
            {
                return;
            }

            ClientAppSettingsService.SourceFileLibraryPath = value;
            StatusMessage = "源文件库位置已保存。";
        }
    }

    public string ApplicantName
    {
        get => _applicantName;
        set => SetProperty(ref _applicantName, value);
    }

    public string DeviceFingerprint => DeviceFingerprintService.CreateFingerprint();

    public string LicenseStatus
    {
        get => _licenseStatus;
        private set => SetProperty(ref _licenseStatus, value);
    }

    public string CurrentVersionText => $"当前版本：{_updateService.CurrentVersion}";

    public string UpdateStatusMessage
    {
        get => _updateStatusMessage;
        private set => SetProperty(ref _updateStatusMessage, value);
    }

    public bool CanInstallUpdate => _latestUpdate?.HasUpdate == true && !string.IsNullOrWhiteSpace(_latestUpdate.AssetDownloadUrl);

    public RelayCommand RefreshCommand { get; }

    public RelayCommand ArchiveCommand { get; }

    public RelayCommand ClearCommand { get; }

    public RelayCommand DownloadCommand { get; }

    public RelayCommand BrowseFileLibraryCacheCommand { get; }

    public RelayCommand BrowseSourceFileLibraryCommand { get; }

    public AsyncRelayCommand SyncFileLibraryCommand { get; }

    public RelayCommand ExportLicenseRequestCommand { get; }

    public RelayCommand ImportActivationCommand { get; }

    public AsyncRelayCommand CheckUpdateCommand { get; }

    public AsyncRelayCommand InstallUpdateCommand { get; }

    public RelayCommand OpenReleasePageCommand { get; }

    public void Refresh()
    {
        Logs.Clear();
        foreach (var entry in OperationLogService.ReadRecent(500))
        {
            Logs.Add(entry);
        }

        Archives.Clear();
        foreach (var file in OperationLogService.ListArchives())
        {
            Archives.Add(new LogArchiveItem(file.Name, file.FullName, file.CreationTime, file.Length));
        }

        var license = _licenseService.ValidateCurrent();
        LicenseStatus = license.IsValid
            ? $"已激活，有效期至 {license.Payload?.ExpiresAtUtc.LocalDateTime:yyyy-MM-dd HH:mm}"
            : license.Message;

        StatusMessage = $"当前日志 {Logs.Count} 条，归档 {Archives.Count} 个。";
    }

    private void ExportLicenseRequest()
    {
        var dialog = new SaveFileDialog
        {
            FileName = $"TenderAssistant-LicenseRequest-{Environment.MachineName}-{DateTime.Now:yyyyMMddHHmmss}.json",
            Filter = "授权申请 JSON (*.json)|*.json",
            Title = "导出离线授权申请文件"
        };

        if (dialog.ShowDialog() == true)
        {
            _licenseService.ExportRequest(ApplicantName, dialog.FileName);
            StatusMessage = $"已导出授权申请：{dialog.FileName}";
            Refresh();
        }
    }

    private void ImportActivation()
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入离线激活文件",
            Filter = "激活文件 (*.bali)|*.bali"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var result = _licenseService.ImportActivation(dialog.FileName);
        StatusMessage = result.Message;
        Refresh();
    }

    private void Archive()
    {
        var archive = OperationLogService.ArchiveCurrent("manual");
        OperationLogService.Info("settings", "archive-log", $"手动归档客户端日志：{archive.Name}。");
        Refresh();
        StatusMessage = $"已归档：{archive.FullName}";
    }

    private void Clear()
    {
        OperationLogService.ClearCurrent();
        OperationLogService.Warning("settings", "clear-log", "手动清空客户端当前日志。");
        Refresh();
        StatusMessage = "当前日志已清空。";
    }

    private void Download()
    {
        var dialog = new SaveFileDialog
        {
            FileName = $"TenderAssistant-Client-Logs-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            Filter = "文本日志 (*.txt)|*.txt",
            Title = "下载客户端日志"
        };

        if (dialog.ShowDialog() == true)
        {
            OperationLogService.ExportCurrent(dialog.FileName);
            OperationLogService.Info("settings", "download-log", $"下载客户端日志：{dialog.FileName}。");
            Refresh();
            StatusMessage = $"日志已下载：{dialog.FileName}";
        }
    }

    private void BrowseFileLibraryCachePath()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择标书辅助本地文件库位置",
            InitialDirectory = Directory.Exists(FileLibraryCachePath)
                ? FileLibraryCachePath
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        if (dialog.ShowDialog() == true)
        {
            FileLibraryCachePath = dialog.FolderName;
            OperationLogService.Info("settings", "change-file-cache", $"标书辅助本地文件库位置改为：{dialog.FolderName}。");
            Refresh();
        }
    }

    private void BrowseSourceFileLibraryPath()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择源文件库位置",
            InitialDirectory = Directory.Exists(SourceFileLibraryPath)
                ? SourceFileLibraryPath
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == true)
        {
            SourceFileLibraryPath = dialog.FolderName;
            OperationLogService.Info("settings", "change-source-file-library", $"源文件库位置改为：{dialog.FolderName}。");
            Refresh();
        }
    }

    private async Task SyncFileLibraryAsync()
    {
        using var status = TaskStatusService.Instance.Begin("正在同步本地文件库...");
        await Task.Yield();
        try
        {
            var result = await Task.Run(() => _fileCatalogService.SyncFromFolder(SourceFileLibraryPath));
            StatusMessage = $"文件库同步完成：复制/更新 {result.Copied} 个，跳过 {result.Skipped} 个。目标：{result.TargetRoot}";
            OperationLogService.Info("settings", "sync-file-library", StatusMessage);
            Refresh();
        }
        catch (Exception ex)
        {
            StatusMessage = $"文件库同步失败：{ex.Message}";
            OperationLogService.Warning("settings", "sync-file-library-failed", StatusMessage);
        }
    }

    private async Task CheckUpdateAsync()
    {
        using var status = TaskStatusService.Instance.Begin("正在检查 GitHub Release 更新...");
        await Task.Yield();
        _latestUpdate = await _updateService.CheckLatestAsync();
        UpdateStatusMessage = _latestUpdate.Message;
        StatusMessage = _latestUpdate.Message;
        InstallUpdateCommand.RaiseCanExecuteChanged();

        if (_latestUpdate.IsSuccess)
        {
            OperationLogService.Info("settings", "check-update", UpdateStatusMessage);
        }
        else
        {
            OperationLogService.Warning("settings", "check-update-failed", UpdateStatusMessage);
        }
    }

    private async Task InstallUpdateAsync()
    {
        if (_latestUpdate is null || !CanInstallUpdate)
        {
            UpdateStatusMessage = "没有可下载的更新安装包。";
            return;
        }

        using var status = TaskStatusService.Instance.Begin("正在下载更新安装包...");
        await Task.Yield();
        try
        {
            var installerPath = await _updateService.DownloadInstallerAsync(_latestUpdate);
            UpdateStatusMessage = $"更新安装包已下载：{installerPath}。安装程序已启动，请按提示完成更新。";
            StatusMessage = UpdateStatusMessage;
            OperationLogService.Info("settings", "download-update", UpdateStatusMessage);
            GitHubUpdateService.LaunchInstaller(installerPath);
        }
        catch (Exception ex)
        {
            UpdateStatusMessage = $"下载或启动更新失败：{ex.Message}";
            StatusMessage = UpdateStatusMessage;
            OperationLogService.Warning("settings", "download-update-failed", UpdateStatusMessage);
        }
    }

    private void OpenReleasePage()
    {
        var releaseUrl = _latestUpdate?.ReleaseUrl ?? GitHubUpdateService.LatestReleasePageUrl;
        GitHubUpdateService.OpenReleasePage(releaseUrl);
        OperationLogService.Info("settings", "open-release-page", $"打开更新源：{releaseUrl}");
    }
}

public sealed record ThemeOption(string Name, ClientApplicationTheme Theme);
