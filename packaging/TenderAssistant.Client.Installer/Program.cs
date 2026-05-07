using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

namespace TenderAssistant.Client.Installer;

internal static class Program
{
    private const string AppName = "投标助手";
    private const string InstalledExeName = "TenderAssistant.Client.exe";

    [STAThread]
    private static int Main()
    {
        try
        {
            string installDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TenderAssistant",
                "Client");

            Directory.CreateDirectory(installDirectory);
            EnsureClientIsNotRunning();
            ExtractResource("TenderAssistant.Client.exe", Path.Combine(installDirectory, InstalledExeName));
            ExtractResource("LICENSE", Path.Combine(installDirectory, "LICENSE"));
            CreateDesktopShortcut(installDirectory);

            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(installDirectory, InstalledExeName),
                WorkingDirectory = installDirectory,
                UseShellExecute = true
            });

            MessageBox.Show("安装完成，已创建桌面快捷方式。", AppName, MessageBoxButton.OK, MessageBoxImage.Information);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"安装失败：{ex.Message}", AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
    }

    private static void EnsureClientIsNotRunning()
    {
        foreach (Process process in Process.GetProcessesByName("TenderAssistant.Client"))
        {
            using (process)
            {
                if (!process.CloseMainWindow())
                {
                    throw new InvalidOperationException("请先关闭正在运行的投标助手客户端，然后重新安装。");
                }

                if (!process.WaitForExit(milliseconds: 5000))
                {
                    throw new InvalidOperationException("投标助手客户端仍在运行，请手动关闭后重新安装。");
                }
            }
        }
    }

    private static void ExtractResource(string resourceSuffix, string destinationPath)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string? resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new InvalidOperationException($"安装包缺少资源：{resourceSuffix}");
        }

        using Stream? input = assembly.GetManifestResourceStream(resourceName);
        if (input is null)
        {
            throw new InvalidOperationException($"无法读取资源：{resourceSuffix}");
        }

        using FileStream output = File.Create(destinationPath);
        input.CopyTo(output);
    }

    private static void CreateDesktopShortcut(string installDirectory)
    {
        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            return;
        }

        object? shell = null;
        object? shortcut = null;

        try
        {
            shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return;
            }

            string shortcutPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                $"{AppName}.lnk");

            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                binder: null,
                target: shell,
                args: [shortcutPath]);

            if (shortcut is null)
            {
                return;
            }

            Type shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, [Path.Combine(installDirectory, InstalledExeName)]);
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, [installDirectory]);
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut))
            {
                Marshal.FinalReleaseComObject(shortcut);
            }

            if (shell is not null && Marshal.IsComObject(shell))
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
