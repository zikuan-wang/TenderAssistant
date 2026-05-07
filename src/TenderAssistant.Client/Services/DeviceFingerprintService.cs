using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace TenderAssistant.Client.Services;

public static class DeviceFingerprintService
{
    public static string CreateFingerprint()
    {
        var components = new[]
        {
            Environment.MachineName,
            Environment.UserDomainName,
            TryReadWmiValue("Win32_ComputerSystemProduct", "UUID"),
            TryReadWmiValue("Win32_BIOS", "SerialNumber")
        };

        var raw = string.Join("|", components.Where(static value => !string.IsNullOrWhiteSpace(value)));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..32];
    }

    private static string TryReadWmiValue(string scope, string propertyName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {propertyName} FROM {scope}");
            foreach (var item in searcher.Get())
            {
                return item[propertyName]?.ToString()?.Trim() ?? string.Empty;
            }
        }
        catch
        {
            return string.Empty;
        }

        return string.Empty;
    }
}
