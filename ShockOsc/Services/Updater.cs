using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenShock.SDK.CSharp.Updatables;
using OpenShock.ShockOsc.Config;
using OpenShock.ShockOsc.Models;
using OpenShock.ShockOsc.Ui.Utils;
using Semver;

namespace OpenShock.ShockOsc.Services;

public sealed class Updater
{
    private const string GithubLatest = "https://api.github.com/repos/OpenShock/ShockOsc/releases/152715042";
    private const string SetupFileName = "ShockOSC_Setup.exe"; // OpenShock.ShockOsc.exe

    private static readonly HttpClient HttpClient = new();

    private readonly string _setupFilePath = Path.Combine(Path.GetTempPath(), SetupFileName);

    private static readonly SemVersion CurrentVersion = SemVersion.Parse(typeof(Updater).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, SemVersionStyles.Strict);

    private Uri? LatestDownloadUrl { get; set; }

    private readonly ILogger<Updater> _logger;
    private readonly ConfigManager _configManager;


    public UpdatableVariable<bool> UpdateAvailable { get; } = new(false);
    public bool IsPostponed { get; private set; }
    public SemVersion? LatestVersion { get; private set; }


    public Updater(ILogger<Updater> logger, ConfigManager configManager)
    {
        _logger = logger;
        _configManager = configManager;
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

    private async Task<(SemVersion, GithubReleaseResponse.Asset)?> GetLatestRelease()
    {
        _logger.LogInformation("Checking GitHub for updates...");

        try
        {
            var res = await HttpClient.GetAsync(GithubLatest);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get latest version information from GitHub. {StatusCode}",
                    res.StatusCode);
                return null;
            }

            var json =
                await JsonSerializer.DeserializeAsync<GithubReleaseResponse>(await res.Content.ReadAsStreamAsync());
            if (json == null)
            {
                _logger.LogWarning("Could not deserialize json");
                return null;
            }

            var tagName = json.TagName;

            if (!SemVersion.TryParse(tagName, SemVersionStyles.AllowV, out var version))
            {
                _logger.LogWarning("Failed to parse version. Value: {Version}", json.TagName);
                return null;
            }

            var asset = json.Assets.FirstOrDefault(x => x.Name.Equals(SetupFileName, StringComparison.InvariantCultureIgnoreCase));
            if (asset == null)
            {
                _logger.LogWarning("Could not find asset with {@SetupName}. Assets found: {@Assets}", SetupFileName,
                    json.Assets);
                return null;
            }
            
            return (version, asset);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to get latest version information from GitHub");
            return null;
        }
    }

    public async Task CheckUpdate()
    {
        IsPostponed = false;
        UpdateAvailable.Value = false;
        
        var latestVersion = await GetLatestRelease();
        if (latestVersion == null)
        {
            UpdateAvailable.Value = false;
            return;
        }

        var comparison = CurrentVersion.ComparePrecedenceTo(latestVersion.Value.Item1);
        if (comparison >= 0)
        {
            _logger.LogInformation("ShockOsc is up to date ([{Version}] >= [{LatestVersion}]) ({Comp})", CurrentVersion,
                latestVersion.Value.Item1, comparison);
            UpdateAvailable.Value = false;
            return;
        }

        UpdateAvailable.Value = true;
        LatestVersion = latestVersion.Value.Item1;
        LatestDownloadUrl = latestVersion.Value.Item2.BrowserDownloadUrl;
        if (_configManager.Config.LastIgnoredVersion != null && _configManager.Config.LastIgnoredVersion.ComparePrecedenceTo(latestVersion.Value.Item1) >= 0)
        {
            _logger.LogInformation(
                "ShockOsc is not up to date. Skipping update due to previous postpone");
            IsPostponed = true;
            return;
        }

        _logger.LogWarning(
            "ShockOsc is not up to date. Newest version is [{NewVersion}] but you are on [{CurrentVersion}]!",
            latestVersion.Value.Item1, CurrentVersion);
    }

    public async Task DoUpdate()
    {
        _logger.LogInformation("Starting update...");
        if (LatestVersion == null || LatestDownloadUrl == null)
        {
            _logger.LogError("LatestVersion or LatestDownloadUrl is null. Cannot update");
            return;
        }

        TryDeleteFile(_setupFilePath);

        _logger.LogDebug("Downloading new release...");
        var sp = Stopwatch.StartNew();
        await using (var stream = await HttpClient.GetStreamAsync(LatestDownloadUrl))
        {
            await using var fStream = new FileStream(_setupFilePath, FileMode.OpenOrCreate);
            await stream.CopyToAsync(fStream);
        }

        _logger.LogDebug("Downloaded file within {TimeTook}ms", sp.ElapsedMilliseconds);
        _logger.LogInformation("Download complete, now restarting to newer application in one second");
        await Task.Delay(1000);
        var startInfo = new ProcessStartInfo
        {
            FileName = _setupFilePath,
            UseShellExecute = true
        };
        Process.Start(startInfo);
        Environment.Exit(0);
    }
}