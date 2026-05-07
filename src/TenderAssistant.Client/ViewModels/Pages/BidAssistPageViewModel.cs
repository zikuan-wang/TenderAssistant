using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using Microsoft.Win32;
using TenderAssistant.Client.Infrastructure;
using TenderAssistant.Client.Models;
using TenderAssistant.Client.Services;

namespace TenderAssistant.Client.ViewModels.Pages;

public sealed class BidAssistPageViewModel : ObservableObject
{
    private readonly BidAssistFileCatalogService _catalogService = new();
    private readonly OfficeDocumentInsertService _insertService = new();
    private readonly OfflineLicenseService _licenseService = new();
    private BidAssistCategory? _selectedCategory;
    private BidAssistFileItem? _selectedFile;
    private PdfQualityOption _selectedPdfQuality;
    private WordInsertModeOption _selectedWordInsertMode;
    private bool _pageBreakBetweenPdfPages = true;
    private double _insertImageWidthCentimeters = 14;
    private string _fileNameFilter = string.Empty;
    private string _statusMessage = "请选择本地文件后插入到当前 Word/WPS 光标位置。";

    public BidAssistPageViewModel()
    {
        PdfQualityOptions =
        [
            new("标准模式", 150, "默认推荐"),
            new("压缩模式", 96, "大文件快速插入"),
            new("高清模式", 200, "正式标书、证书类文件")
        ];
        WordInsertModeOptions =
        [
            new("保留格式插入", "keep-format"),
            new("纯文本插入", "plain-text")
        ];
        _selectedPdfQuality = PdfQualityOptions[0];
        _selectedWordInsertMode = WordInsertModeOptions[0];

        ImportCustomCommand = new RelayCommand(_ => ImportCustomFiles());
        ClearCustomCommand = new RelayCommand(_ => ClearCustomFiles(), _ => IsCustomCategorySelected && AllFiles.Any(static item => item.CategoryCode == "custom"));
        RefreshCommand = new RelayCommand(_ => RefreshLibrary());
        OpenSourceCommand = new RelayCommand(_ => OpenSourceFile(), _ => SelectedFile is not null);
        OpenLocalFolderCommand = new RelayCommand(_ => OpenLocalFolder(), _ => SelectedFile is not null);
        InsertSelectedCommand = new AsyncRelayCommand(InsertSelectedAsync, () => SelectedFile is not null);
        DeleteLocalCacheCommand = new RelayCommand(_ => DeleteLocalCache(), _ => SelectedFile?.SyncToLocal == true);

        foreach (var category in _catalogService.GetCategories())
        {
            Categories.Add(category);
        }

        SelectedCategory = Categories.FirstOrDefault();
        RefreshLibrary();
        OperationLogService.Info("bid-assist", "open", "打开标书辅助页面。");
    }

    public ObservableCollection<BidAssistCategory> Categories { get; } = new();

    public ObservableCollection<BidAssistFileItem> Files { get; } = new();

    public ObservableCollection<BidAssistInsertLogItem> InsertLogs { get; } = new();

    public IReadOnlyList<PdfQualityOption> PdfQualityOptions { get; }

    public IReadOnlyList<WordInsertModeOption> WordInsertModeOptions { get; }

    public BidAssistCategory? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                RefreshVisibleFiles();
                OnPropertyChanged(nameof(IsCustomCategorySelected));
                ClearCustomCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public BidAssistFileItem? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (SetProperty(ref _selectedFile, value))
            {
                OpenSourceCommand.RaiseCanExecuteChanged();
                OpenLocalFolderCommand.RaiseCanExecuteChanged();
                InsertSelectedCommand.RaiseCanExecuteChanged();
                DeleteLocalCacheCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(PreviewText));
                OnPropertyChanged(nameof(SelectedFilePath));
                OnPropertyChanged(nameof(PreviewImagePath));
                OnPropertyChanged(nameof(HasImagePreview));
                OnPropertyChanged(nameof(HasTextPreview));
                OnPropertyChanged(nameof(IsCustomCategorySelected));
            }
        }
    }

    public PdfQualityOption SelectedPdfQuality
    {
        get => _selectedPdfQuality;
        set => SetProperty(ref _selectedPdfQuality, value);
    }

    public WordInsertModeOption SelectedWordInsertMode
    {
        get => _selectedWordInsertMode;
        set => SetProperty(ref _selectedWordInsertMode, value);
    }

    public bool PageBreakBetweenPdfPages
    {
        get => _pageBreakBetweenPdfPages;
        set => SetProperty(ref _pageBreakBetweenPdfPages, value);
    }

    public double InsertImageWidthCentimeters
    {
        get => _insertImageWidthCentimeters;
        set => SetProperty(ref _insertImageWidthCentimeters, Math.Clamp(value, 1, 40));
    }

    public string FileNameFilter
    {
        get => _fileNameFilter;
        set
        {
            if (SetProperty(ref _fileNameFilter, value))
            {
                RefreshVisibleFiles();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string PreviewText => SelectedFile?.PreviewText ?? "选择左侧或中间列表中的文件后显示预览信息。";

    public string SelectedFilePath => SelectedFile?.FullPath ?? string.Empty;

    public string PreviewImagePath => SelectedFile?.PreviewImagePath ?? string.Empty;

    public bool HasImagePreview => SelectedFile?.HasImagePreview == true;

    public bool HasTextPreview => !HasImagePreview;

    public bool IsCustomCategorySelected => string.Equals(SelectedCategory?.Code, "custom", StringComparison.OrdinalIgnoreCase);

    public bool IsActivated => _licenseService.ValidateCurrent().IsValid;

    public RelayCommand ImportCustomCommand { get; }

    public RelayCommand ClearCustomCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand OpenSourceCommand { get; }

    public RelayCommand OpenLocalFolderCommand { get; }

    public AsyncRelayCommand InsertSelectedCommand { get; }

    public RelayCommand DeleteLocalCacheCommand { get; }

    private List<BidAssistFileItem> AllFiles { get; } = new();

    private void RefreshLibrary()
    {
        AllFiles.RemoveAll(static item => item.CategoryCode != "custom");
        AllFiles.AddRange(_catalogService.LoadLibraryFiles());
        RefreshVisibleFiles();
        StatusMessage = IsActivated
            ? $"已刷新本地文件库，共 {AllFiles.Count} 个文件。"
            : "软件尚未激活。请在设置与日志中导出离线授权申请，导入激活文件后再使用插入功能。";
        OperationLogService.Info("bid-assist", "refresh-library", StatusMessage);
    }

    private void RefreshVisibleFiles()
    {
        Files.Clear();
        var categoryCode = SelectedCategory?.Code;
        foreach (var item in AllFiles.Where(item => MatchesVisibleFilter(item, categoryCode)))
        {
            Files.Add(item);
        }

        SelectedFile = Files.FirstOrDefault();
        ClearCustomCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(IsCustomCategorySelected));
    }

    private bool MatchesVisibleFilter(BidAssistFileItem item, string? categoryCode)
    {
        if (categoryCode is not null && item.CategoryCode != categoryCode)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(FileNameFilter)
            && !item.FileName.Contains(FileNameFilter.Trim(), StringComparison.CurrentCultureIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private void ClearCustomFiles()
    {
        var removed = AllFiles.RemoveAll(static item => item.CategoryCode == "custom");
        RefreshVisibleFiles();
        StatusMessage = $"已清除自定义导入列表 {removed} 项。";
        OperationLogService.Warning("bid-assist", "clear-custom", StatusMessage);
    }

    private void DeleteLocalCache()
    {
        if (SelectedFile is null)
        {
            return;
        }

        var fileName = SelectedFile.FileName;
        if (!_catalogService.DeleteLocalCache(SelectedFile))
        {
            StatusMessage = "当前文件不是本地文件库文件，不能在此处删除。";
            return;
        }

        AllFiles.Remove(SelectedFile);
        RefreshVisibleFiles();
        StatusMessage = $"已删除本地文件：{fileName}。";
        OperationLogService.Warning("bid-assist", "delete-local-cache", StatusMessage);
    }

    private void ImportCustomFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择要导入的标书辅助文件",
            Filter = "支持的文件|*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.doc;*.docx;*.rtf;*.txt|所有文件|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var imported = 0;
        foreach (var fileName in dialog.FileNames)
        {
            var item = _catalogService.CreateCustomFile(fileName);
            if (item is null || AllFiles.Any(existing => string.Equals(existing.FullPath, item.FullPath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            AllFiles.Add(item);
            imported++;
        }

        SelectedCategory = Categories.Single(static item => item.Code == "custom");
        RefreshVisibleFiles();
        StatusMessage = $"已导入 {imported} 个自定义文件。";
        OperationLogService.Info("bid-assist", "import-custom", StatusMessage);
    }

    private void OpenSourceFile()
    {
        if (SelectedFile is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(SelectedFile.FullPath) { UseShellExecute = true });
            OperationLogService.Info("bid-assist", "open-source", $"打开源文件：{SelectedFile.FileName}。");
        }
        catch (Exception ex)
        {
            StatusMessage = $"打开源文件失败：{ex.Message}";
            OperationLogService.Warning("bid-assist", "open-source-failed", StatusMessage);
        }
    }

    private void OpenLocalFolder()
    {
        if (SelectedFile is null)
        {
            return;
        }

        try
        {
            var argument = File.Exists(SelectedFile.FullPath)
                ? $"/select,\"{SelectedFile.FullPath}\""
                : $"\"{Path.GetDirectoryName(SelectedFile.FullPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\"";
            Process.Start(new ProcessStartInfo("explorer.exe", argument) { UseShellExecute = true });
            OperationLogService.Info("bid-assist", "open-local-folder", $"打开文件本地位置：{SelectedFile.FileName}。");
        }
        catch (Exception ex)
        {
            StatusMessage = $"打开本地位置失败：{ex.Message}";
            OperationLogService.Warning("bid-assist", "open-local-folder-failed", StatusMessage);
        }
    }

    private async Task InsertSelectedAsync()
    {
        if (!EnsureActivated())
        {
            return;
        }

        if (SelectedFile is null)
        {
            return;
        }

        using var status = TaskStatusService.Instance.Begin($"正在插入：{SelectedFile.FileName}");
        await Dispatcher.Yield(DispatcherPriority.Render);
        InsertOne(SelectedFile);
    }

    private bool EnsureActivated()
    {
        var license = _licenseService.ValidateCurrent();
        if (license.IsValid)
        {
            return true;
        }

        StatusMessage = license.Message;
        OperationLogService.Warning("license", "blocked", $"授权校验未通过，阻止插入操作：{license.Message}");
        return false;
    }

    private void InsertOne(BidAssistFileItem item)
    {
        var result = _insertService.Insert(item, SelectedPdfQuality, SelectedWordInsertMode, PageBreakBetweenPdfPages, InsertImageWidthCentimeters);
        var status = result.IsSuccess ? "成功" : "失败";
        InsertLogs.Insert(0, new BidAssistInsertLogItem(DateTime.Now, item.FileName, result.TargetSoftware, status, result.Message));
        StatusMessage = $"{item.FileName}：{status}。{result.Message}";
        var logMessage = $"插入日志 | 文件：{item.FileName} | 目标软件：{result.TargetSoftware} | 结果：{status} | 说明：{result.Message}";

        if (result.IsSuccess)
        {
            OperationLogService.Info("bid-assist", "insert-log", logMessage);
        }
        else
        {
            OperationLogService.Warning("bid-assist", "insert-log", logMessage);
        }
    }
}
