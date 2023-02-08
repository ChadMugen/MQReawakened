﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Server.Base.Core.Abstractions;
using Server.Base.Core.Events;
using Server.Base.Core.Extensions;
using Server.Base.Core.Helpers;
using Server.Base.Core.Models;
using Server.Base.Core.Services;
using Server.Base.Worlds;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Web.Launcher.Models;
using Web.Launcher.Models.Current;

namespace Web.Launcher.Services;

public class StartGame : IService
{
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ServerConsole _console;
    private readonly LauncherConfig _lConfig;
    private readonly ILogger<StartGame> _logger;
    private readonly SettingsConfig _sConfig;
    private readonly EventSink _sink;
    private string _directory;
    private bool _dirSet, _appStart;
    private Process _game;
    private World _world;

    public PackageInformation CurrentVersion { get; private set; }
    public List<string> Assets { get; set; }

    public StartGame(EventSink sink, LauncherConfig lConfig, SettingsConfig sConfig,
        IHostApplicationLifetime appLifetime, ILogger<StartGame> logger, ServerConsole console, World world)
    {
        _sink = sink;
        _lConfig = lConfig;
        _sConfig = sConfig;
        _appLifetime = appLifetime;
        _logger = logger;
        _console = console;
        _world = world;

        _dirSet = false;
        _appStart = false;

        Assets = new List<string>();
    }

    public void Initialize()
    {
        _appLifetime.ApplicationStarted.Register(AppStarted);
        _sink.WorldLoad += GetGameInformation;
        _sink.Shutdown += StopGame;
    }

    private void StopGame() => _game?.CloseMainWindow();

    private void AppStarted()
    {
        _appStart = true;
        RunGame();
    }

    private void GetGameInformation()
    {
        _console.AddCommand(new ConsoleCommand("runLauncher",
            "Runs the launcher and hooks it into the current process.",
            _ => LaunchGame()));

        _logger.LogInformation("Getting Game Executable");

        try
        {
            _lConfig.GameSettingsFile = SetFileValue.SetIfNotNull(_lConfig.GameSettingsFile, "Get Settings File",
                "Settings File (*.txt)\0*.txt\0");

            _sConfig.WriteToSettings(_lConfig);
        }
        catch
        {
            // ignored
        }

        while (true)
        {
            if (string.IsNullOrEmpty(_lConfig.GameSettingsFile) || !_lConfig.GameSettingsFile.EndsWith("settings.txt"))
            {
                _logger.LogError("Please enter the absolute file path for your game's 'settings.txt' file.");
                _lConfig.GameSettingsFile = Console.ReadLine();
                continue;
            }

            _directory = Path.GetDirectoryName(_lConfig.GameSettingsFile);

            if (string.IsNullOrEmpty(_directory))
                continue;

            CurrentVersion =
                JsonSerializer.Deserialize<PackageInformation>(File.ReadAllText(Path.Join(_directory, "current.txt")));

            break;
        }

        _logger.LogDebug("Got launcher directory: {Directory}", Path.GetDirectoryName(_lConfig.GameSettingsFile));

        var lastUpdate = DateTime.ParseExact(CurrentVersion.game.lastUpdate, _lConfig.TimeFilter,
            CultureInfo.InvariantCulture);
        var lastOldClientUpdate = DateTime.ParseExact(_lConfig.OldClientLastUpdate, _lConfig.TimeFilter,
            CultureInfo.InvariantCulture);

        _lConfig.Is2014Client = lastUpdate > lastOldClientUpdate;

        if (!_lConfig.Is2014Client)
            _directory = new DirectoryInfo(_directory).Parent?.FullName;

        _lConfig.LastClientUpdate = lastUpdate.ToUnixTimestamp();

        _dirSet = true;

        RunGame();
    }

    public void AskIfRestart()
    {
        if (!_lConfig.StartLauncherOnCommand)
            if (_logger.Ask("The launcher is not set to restart on a related command being run, " +
                            "would you like to enable this?", true))
                _lConfig.StartLauncherOnCommand = true;

        if (_lConfig.StartLauncherOnCommand)
            LaunchGame();
    }

    private void RunGame()
    {
        if (!_appStart || !_dirSet)
            return;

        if (_lConfig.OverwriteGameConfig)
            WriteConfig();

        if (!_world.Crashed)
            LaunchGame();
    }

    public void LaunchGame()
    {
        Assets.Clear();
        _game = Process.Start(Path.Join(_directory, "launcher", "launcher.exe"));
        _logger.LogDebug("Running game on process: {GamePath}", _game?.ProcessName);
    }

    private void WriteConfig()
    {
        var directory = Path.Join(_directory, "game");
        var config = Path.Join(directory, "LocalBuildConfig.xml");

        _logger.LogInformation("Looking For Header In {Directory} Ending In {Header}.", directory,
            _lConfig.HeaderFolderFilter);

        var parentUri = new Uri(directory);
        var headerFolders = Directory.GetDirectories(directory, string.Empty, SearchOption.AllDirectories)
            .Select(d => Path.GetDirectoryName(d)?.ToLower())
            .Where(d => new Uri(new DirectoryInfo(d!).Parent?.FullName!) == parentUri).ToArray();

        var headerFolder = headerFolders.FirstOrDefault(a => a?.EndsWith(_lConfig.HeaderFolderFilter) == true);
        headerFolder = Path.GetFileName(headerFolder?.Remove(headerFolder.Length - _lConfig.HeaderFolderFilter.Length));

        _logger.LogDebug("Found header: {Header}", headerFolder);

        _logger.LogInformation("Writing Build Config To {Place}", config);

        var newDoc = new XDocument();
        var root = new XElement("MQBuildConfg");

        foreach (var item in GetConfigValues(headerFolder))
        {
            var xmlItem = new XElement("item");
            xmlItem.Add(new XAttribute("name", item.Key));
            xmlItem.Add(new XAttribute("value", item.Value));
            root.Add(xmlItem);
        }

        newDoc.Add(root);
        newDoc.Save(config);

        _logger.LogDebug("Written build configuration");
    }

    private Dictionary<string, string> GetConfigValues(string header) => new()
    {
        { $"{header}.unity.url.membership", $"{_lConfig.BaseUrl}/Membership" },
        { $"{header}.unity.cache.domain", $"{_lConfig.BaseUrl}/Cache" },
        { $"{header}.unity.cache.license", $"{_lConfig.CacheLicense}" },
        { $"{header}.unity.cache.size", _lConfig.CacheSize.ToString() },
        { $"{header}.unity.cache.expiration", _lConfig.CacheExpiration.ToString() },
        { "game.cacheversion", _lConfig.CacheVersion.ToString() },
        { $"{header}.unity.url.crisp.host", $"{_lConfig.BaseUrl}/Chat/" },
        { "asset.log", _lConfig.LogAssets ? "true" : "false" },
        { "asset.disableversioning", _lConfig.DisableVersions ? "true" : "false" },
        { "asset.jboss", $"{_lConfig.BaseUrl}/Apps/" },
        { "asset.bundle", $"{_lConfig.BaseUrl}/Client/Bundles" },
        { "asset.audio", $"{_lConfig.BaseUrl}/Client/Audio" },
        { "logout.url", $"{_lConfig.BaseUrl}/Logout" },
        { "contactus.url", $"{_lConfig.BaseUrl}/Contact" },
        { "tools.urlbase", $"{_lConfig.BaseUrl}/Tools/" },
        { "leaderboard.domain", $"{_lConfig.BaseUrl}/Apps/" },
        { "analytics.baseurl", $"{_lConfig.BaseUrl}/Analytics/" },
        { "analytics.enabled", _lConfig.AnalyticsEnabled ? "true" : "false" },
        { "analytics.apikey", _lConfig.AnalyticsApiKey },
        { "project.name", _lConfig.ProjectName }
    };
}
