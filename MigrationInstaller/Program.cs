using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;
using OpenShock.ShockOSC.MigrationInstaller.Schemas;

namespace OpenShock.ShockOSC.MigrationInstaller;

public static class Program
{
    private const string AppDisplayName = "ShockOSC";
    private const string DownloadUrl = "https://github.com/OpenShock/Desktop/releases/latest/download/OpenShock_Desktop_Setup.exe";
    private static string TempInstallerPath => Path.Combine(Path.GetTempPath(), "OpenShock_Desktop_Setup.exe");
    
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string DesktopProgramPath = Path.Combine(LocalAppData, "OpenShock", "Desktop");
    private static readonly string DesktopExePath = Path.Combine(DesktopProgramPath, "OpenShock.Desktop.exe");

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
        
        try
        {
            await MigrateConfig();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Config migration failed: {ex.Message}");
        }
        
        Console.WriteLine("ℹ️ Killing ShockOSC process if running...");
        var processes = Process.GetProcessesByName("OpenShock.ShockOsc");
        if (processes.Length > 0)
        {
            foreach (var process in processes)
            {
                Console.WriteLine($"🛑 Killing process {process.ProcessName} (ID: {process.Id})");
                process.Kill();
                await process.WaitForExitAsync();
                Console.WriteLine($"✅ Process {process.ProcessName} (ID: {process.Id}) killed.");
            }
        }
        else
        {
            Console.WriteLine("ℹ️ No ShockOSC processes found.");
        }
        
        Console.WriteLine("🔍 Searching for uninstaller...");
        var uninstallerPath = FindUninstaller(AppDisplayName);
        if (string.IsNullOrEmpty(uninstallerPath))
        {
            Console.WriteLine("❌ Uninstaller not found.");
            return false;
        }

        Console.WriteLine($"🗑 Running uninstaller: {uninstallerPath}");
        await RunProcess(uninstallerPath, "/S");

        Console.WriteLine("🌐 Downloading new installer...");
        await DownloadFile(DownloadUrl, TempInstallerPath);

        Console.WriteLine("🚀 Launching new installer... Please wait a few seconds for the installation to complete....");
        await RunProcess(TempInstallerPath, "/S");
        Console.WriteLine("✅ Update process complete.");
        
        Console.WriteLine($"🔄 Relaunching OpenShock Desktop... ({DesktopExePath})");
        
        var proc = new Process();
        proc.StartInfo.WorkingDirectory = DesktopProgramPath;
        proc.StartInfo.FileName = DesktopExePath;
        proc.StartInfo.UseShellExecute = true;
        proc.Start();

        Console.WriteLine("✅ Done. Closing updater in 5 seconds...");

        await Task.Delay(5000);
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

    private static async Task RunProcess(string file, string args)
    {
        var proc = new Process();
        proc.StartInfo.FileName = file;
        proc.StartInfo.Arguments = args;
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.CreateNoWindow = false;
        proc.Start();
        await proc.WaitForExitAsync();
    }
    
    private static bool IsRunAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    #region Migration

    private static readonly string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static readonly string OldConfigFolder = Path.Combine(AppData, "OpenShock", "ShockOSC");
    private static readonly string OldConfigPath = Path.Combine(OldConfigFolder, "config.json");
    private static readonly string ModuleDataFolder = Path.Combine(AppData, "OpenShock", "Desktop", "moduleData", "openshock.shockosc");
    private static readonly string ModuleConfig = Path.Combine(ModuleDataFolder, "config.json");
    
    private static async Task MigrateConfig()
    {
        if (!File.Exists(OldConfigPath))
        {
            Console.WriteLine($"ℹ️ No old config found to migrate. Checked at {OldConfigPath}");
            return;
        }
        
        Directory.CreateDirectory(ModuleDataFolder);

        if (File.Exists(ModuleConfig))
        {
            Console.WriteLine("ℹ️ New config already exists, skipping migration.");
            return;
        }

        Console.WriteLine($"🔄 Migrating config from {OldConfigPath} -> {ModuleConfig}");

        var json = await File.ReadAllTextAsync(OldConfigPath);
        File.Move(OldConfigPath, $"{OldConfigPath}.bak", true);
        OldSchema.ShockOscConfig? oldConfig;
        try
        {
            oldConfig = JsonSerializer.Deserialize<OldSchema.ShockOscConfig>(json, OldSchemaSourceGenerationContext.Default.ShockOscConfig);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to deserialize old config: {ex.Message}");
            return;
        }

        if (oldConfig == null)
        {
            Console.WriteLine("❌ Old config deserialized to null, aborting migration.");
            return;
        }

        var newConfig = oldConfig.ConvertToNew();
        var newJson = JsonSerializer.Serialize(newConfig, NewSchemaSourceGenerationContext.Default.ShockOscConfig);

        await File.WriteAllTextAsync(ModuleConfig, newJson);
        Console.WriteLine("✅ Config migration complete.");
    }
    
    #endregion
}