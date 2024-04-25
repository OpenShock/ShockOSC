using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenShock.ShockOsc.Config;
using OpenShock.ShockOsc.Models;
using OpenShock.ShockOsc.Ui.Utils;

namespace OpenShock.ShockOsc.Services;

public sealed class Updater
{
    private const string GithubLatest = "https://api.github.com/repos/OpenShock/ShockOsc/releases/latest";
    private const string SetupFileName = "ShockOSC_Setup.exe"; // OpenShock.ShockOsc.exe

    private static readonly HttpClient HttpClient = new();

    private readonly string _setupFilePath = Path.Combine(Environment.CurrentDirectory, SetupFileName);

    private readonly Version _currentVersion = Assembly.GetEntryAssembly()?.GetName().Version ??
                                               throw new Exception("Could not determine ShockOsc version");

    private Uri? LatestDownloadUrl { get; set; }

    private readonly ILogger<Updater> _logger;
    private readonly ConfigManager _configManager;


    public UpdateableVariable<bool> UpdateAvailable { get; } = new(false);
    public Version? LatestVersion { get; private set; }


    public Updater(ILogger<Updater> logger, ConfigManager configManager)
    {
        _logger = logger;
        _configManager = configManager;
        HttpClient.DefaultRequestHeaders.Add("User-Agent", $"ShockOsc/{_currentVersion}");
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

    private async Task<(Version, GithubReleaseResponse.Asset)?> GetLatestRelease()
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
            if (!string.IsNullOrEmpty(tagName) && tagName[0] == 'v')
                tagName = tagName[1..];

            if (!Version.TryParse(tagName, out var version))
            {
                _logger.LogWarning("Failed to parse version. Value: {Version}", json.TagName);
                return null;
            }

            var asset = json.Assets.FirstOrDefault(x => x.Name == SetupFileName);
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
        var latestVersion = await GetLatestRelease();
        if (latestVersion == null)
        {
            UpdateAvailable.Value = false;
            return;
        }
        if (latestVersion.Value.Item1 <= _currentVersion)
        {
            _logger.LogInformation("ShockOsc is up to date ([{Version}] >= [{LatestVersion}])", _currentVersion,
                latestVersion.Value.Item1);
            UpdateAvailable.Value = false;
            return;
        }

        UpdateAvailable.Value = true;
        LatestVersion = latestVersion.Value.Item1;
        LatestDownloadUrl = latestVersion.Value.Item2.BrowserDownloadUrl;
        if (_configManager.Config.LastIgnoredVersion != null &&
            _configManager.Config.LastIgnoredVersion >= latestVersion.Value.Item1)
        {
            _logger.LogInformation(
                "ShockOsc is not up to date. Skipping update due to previous postpone. You can reenable the updater by setting 'LastIgnoredVersion' to null");
            UpdateAvailable.Value = false;
            return;
        }

        _logger.LogWarning(
            "ShockOsc is not up to date. Newest version is [{NewVersion}] but you are on [{CurrentVersion}]!",
            latestVersion.Value.Item1, _currentVersion);

        UpdateAvailable.Value = true;
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