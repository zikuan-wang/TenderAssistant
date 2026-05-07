using System.IO;
using TenderAssistant.Licensing;

namespace TenderAssistant.Client.Services;

public sealed class OfflineLicenseService
{
    private static readonly string LicenseDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TenderAssistant");

    private static readonly string LicensePath = Path.Combine(LicenseDirectory, "license.bali");

    public string LicenseFilePath => LicensePath;

    public static event EventHandler? LicenseChanged;

    public OfflineLicenseRequest CreateRequest(string applicantName)
    {
        return new OfflineLicenseRequest(
            Guid.NewGuid(),
            string.IsNullOrWhiteSpace(applicantName) ? Environment.UserName : applicantName.Trim(),
            Environment.MachineName,
            DeviceFingerprintService.CreateFingerprint(),
            DateTimeOffset.UtcNow);
    }

    public void ExportRequest(string applicantName, string path)
    {
        var request = CreateRequest(applicantName);
        File.WriteAllText(path, OfflineLicenseJson.SerializeRequest(request));
        OperationLogService.Info("license", "export-request", $"导出离线授权申请文件：{path}。");
    }

    public LicenseValidationResult ImportActivation(string path)
    {
        var envelope = OfflineLicenseCrypto.DecryptEnvelope(File.ReadAllText(path))
            ?? throw new InvalidOperationException("激活文件格式无效。");

        var result = OfflineLicenseCrypto.Validate(envelope, DeviceFingerprintService.CreateFingerprint());
        if (!result.IsValid)
        {
            OperationLogService.Warning("license", "import-activation-failed", result.Message);
            return result;
        }

        Directory.CreateDirectory(LicenseDirectory);
        File.Copy(path, LicensePath, true);
        OperationLogService.Info("license", "import-activation", $"导入离线激活文件：{path}。");
        LicenseChanged?.Invoke(null, EventArgs.Empty);
        return result;
    }

    public LicenseValidationResult ValidateCurrent()
    {
        if (!File.Exists(LicensePath))
        {
            return new LicenseValidationResult(false, "not_activated", "软件尚未激活。", null, DateTimeOffset.UtcNow);
        }

        try
        {
            var envelope = OfflineLicenseCrypto.DecryptEnvelope(File.ReadAllText(LicensePath));
            return envelope is null
                ? new LicenseValidationResult(false, "invalid_file", "本机授权文件格式无效。", null, DateTimeOffset.UtcNow)
                : OfflineLicenseCrypto.Validate(envelope, DeviceFingerprintService.CreateFingerprint());
        }
        catch (Exception ex)
        {
            return new LicenseValidationResult(false, "read_failed", $"读取本机授权失败：{ex.Message}", null, DateTimeOffset.UtcNow);
        }
    }
}
