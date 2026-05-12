using System.Reflection;

namespace TenderAssistant.Client.Services;

public static class AppVersionService
{
    public static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    public static string CurrentVersionText => Format(CurrentVersion);

    public static string Format(Version? version)
    {
        if (version is null)
        {
            return "0.0.0";
        }

        return $"{Math.Max(0, version.Major)}.{Math.Max(0, version.Minor)}.{Math.Max(0, version.Build)}";
    }
}
