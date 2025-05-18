using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;

namespace MigrationInstaller;

public static class Program
{
    private const string AppDisplayName = "ShockOSC";
    private const string DownloadUrl = "https://github.com/OpenShock/Desktop/releases/download/1.0.0-preview.4/OpenShock_Desktop_Setup.exe";
    private static string TempInstallerPath => Path.Combine(Path.GetTempPath(), "OpenShock_Desktop_Setup.exe");

    public static async Task Main()
    {
        try
        {
            if (await RunMainLogic()) return;
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Error: " + ex.Message);
        }
        
        Console.WriteLine("Press enter to exit.");
        Console.ReadLine();
    }

    private static bool RelaunchAsAdmin()
    {
        // Relaunch with admin rights
        var processInfo = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath,
            UseShellExecute = true,
            Verb = "runas"
        };

        try
        {
            Process.Start(processInfo);
            return true;
        }
        catch
        {
            Console.WriteLine("User denied elevation.");
            return false;
        }
    }
    
    private static async Task<bool> RunMainLogic()
    {
        if(!IsRunAsAdmin()) 
        {
            Console.WriteLine("❌ Please run this program as administrator.");
            return RelaunchAsAdmin();
        }
        
        Console.WriteLine("🔍 Searching for uninstaller...");
        var uninstallerPath = FindUninstaller(AppDisplayName);
        if (string.IsNullOrEmpty(uninstallerPath))
        {
            Console.WriteLine("❌ Uninstaller not found.");
            return false;
        }

        Console.WriteLine($"🗑 Running uninstaller: {uninstallerPath}");
        RunProcess(uninstallerPath, "/S");

        Console.WriteLine("🌐 Downloading new installer...");
        await DownloadFile(DownloadUrl, TempInstallerPath);

        Console.WriteLine("🚀 Launching new installer...");
        RunProcess(TempInstallerPath, "");

        Console.WriteLine("✅ Update process complete.");
        return true;
    }

    private static readonly ImmutableArray<string> RegistryPaths =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    ];

    private static readonly ImmutableArray<RegistryKey> Keys =
    [
        Registry.LocalMachine,
        Registry.CurrentUser
    ];

    private static string? FindUninstaller(string displayName)
    {
        foreach (var registryKey in Keys)
        {
            foreach (var reg in RegistryPaths)
            {
                using var key = registryKey.OpenSubKey(reg);
                if (key == null) continue;
                var result = CheckForUninstallString(displayName, key);
                if (!string.IsNullOrEmpty(result)) return result;
            }
        }
        
        return null;
    }

    private static string? CheckForUninstallString(string displayName, RegistryKey? key)
    {
        if (key == null) return null; // continue;
        foreach (var subkeyName in key.GetSubKeyNames())
        {
            using var subkey = key.OpenSubKey(subkeyName);
            if(subkey?.GetValue("DisplayName") is not string regDisplayName) continue;
            if (!regDisplayName.Equals(displayName, StringComparison.InvariantCulture)) continue;
            if(subkey.GetValue("UninstallString") is not string regUninstallString) continue;
            return regUninstallString;
        }

        return null;
    }

    private static async Task DownloadFile(string url, string destinationPath)
    {
        Console.WriteLine($"Downloading from {url} to {destinationPath}");
        using var client = new HttpClient();
        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        await using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
        await response.Content.CopyToAsync(fs);
    }

    private static void RunProcess(string file, string args)
    {
        var proc = new Process();
        proc.StartInfo.FileName = file;
        proc.StartInfo.Arguments = args;
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.CreateNoWindow = true;
        proc.Start();
        proc.WaitForExit();
    }
    
    private static bool IsRunAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}