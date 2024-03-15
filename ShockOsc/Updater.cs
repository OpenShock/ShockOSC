using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using OpenShock.ShockOsc.Models;
using Serilog;

namespace OpenShock.ShockOsc;

public static class Updater
{
    private static readonly ILogger Logger = Log.ForContext(typeof(Updater));
    private static readonly HttpClient HttpClient = new();
    private const string GithubLatest = "https://api.github.com/repos/OpenShock/ShockOsc/releases/latest";
    private const string SetupFileName = "ShockOSC_Setup.exe"; // OpenShock.ShockOsc.exe
    private static readonly string SetupFilePath = Path.Combine(Environment.CurrentDirectory, SetupFileName);
    private static readonly Version CurrentVersion = Assembly.GetEntryAssembly()?.GetName().Version ?? throw new Exception("Could not determine ShockOsc version");

    public static bool UpdateAvailable { get; private set; }
    public static Version? LatestVersion { get; private set; }
    public static Uri? LatestDownloadUrl { get; private set; }

    static Updater()
    {
        HttpClient.DefaultRequestHeaders.Add("User-Agent", $"ShockOsc/{CurrentVersion}");
    }

    private static bool TryDeleteFile(string fileName)
    {
        if (!File.Exists(fileName)) return false;
        try
        {
            File.Delete(fileName);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    private static async Task<(Version, GithubReleaseResponse.Asset)?> GetLatestRelease()
    {
        Logger.Information("Checking GitHub for updates...");

        try
        {
            var res = await HttpClient.GetAsync(GithubLatest);
            if (!res.IsSuccessStatusCode)
            {
                Logger.Warning("Failed to get latest version information from GitHub. {StatusCode}", res.StatusCode);
                return null;
            }

            var json = await JsonSerializer.DeserializeAsync<GithubReleaseResponse>(await res.Content.ReadAsStreamAsync());
            if (json == null)
            {
                Logger.Warning("Could not deserialize json");
                return null;
            }

            if (!Version.TryParse(json.TagName[1..], out var version))
            {
                Logger.Warning("Failed to parse version. Value: {Version}", json.TagName);
                return null;
            }

            var asset = json.Assets.FirstOrDefault(x => x.Name == SetupFileName);
            if (asset == null)
            {
                Logger.Warning("Could not find asset with {@SetupName}. Assets found: {@Assets}", SetupFileName, json.Assets);
                return null;
            }

            return (version, asset);
        }
        catch (Exception e)
        {
            Logger.Warning(e, "Failed to get latest version information from GitHub");
            return null;
        }
    }

    public static async Task<bool> CheckUpdate()
    {
        var latestVersion = await GetLatestRelease();
        if (latestVersion is null) return false;
        if (latestVersion.Value.Item1 <= CurrentVersion)
        {
            Logger.Information("ShockOsc is up to date ([{Version}] >= [{LatestVersion}])", CurrentVersion, latestVersion.Value.Item1);
            UpdateAvailable = false;
            return false;
        }

        UpdateAvailable = true;
        LatestVersion = latestVersion.Value.Item1;
        LatestDownloadUrl = latestVersion.Value.Item2.BrowserDownloadUrl;
        if (Config.ConfigInstance.LastIgnoredVersion != null &&
            Config.ConfigInstance.LastIgnoredVersion >= latestVersion.Value.Item1)
        {
            Logger.Information("ShockOsc is not up to date. Skipping update due to previous postpone. You can reenable the updater by setting 'LastIgnoredVersion' to null");
            return false;
        }
        
        Logger.Warning(
            "ShockOsc is not up to date. Newest version is [{NewVersion}] but you are on [{CurrentVersion}]!",
            latestVersion.Value.Item1, CurrentVersion);

        return true;
    }

    public static async Task DoUpdate()
    {
        Logger.Information("Starting update...");
        if (LatestVersion == null || LatestDownloadUrl == null)
        {
            Logger.Error("LatestVersion or LatestDownloadUrl is null. Cannot update");
            return;
        }

        TryDeleteFile(SetupFilePath);

        Logger.Debug("Downloading new release...");
        var sp = Stopwatch.StartNew();
        await using (var stream = await HttpClient.GetStreamAsync(LatestDownloadUrl))
        {
            await using var fStream = new FileStream(SetupFilePath, FileMode.OpenOrCreate);
            await stream.CopyToAsync(fStream);
        }

        Logger.Debug("Downloaded file within {TimeTook}ms", sp.ElapsedMilliseconds);
        Logger.Information("Download complete, now restarting to newer application in one second");
        await Task.Delay(1000);
        var startInfo = new ProcessStartInfo
        {
            FileName = SetupFilePath,
            UseShellExecute = true
        };
        Process.Start(startInfo);
        Environment.Exit(0);
    }
}