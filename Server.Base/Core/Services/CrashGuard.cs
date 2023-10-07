﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Server.Base.Accounts.Models;
using Server.Base.Core.Abstractions;
using Server.Base.Core.Configs;
using Server.Base.Core.Events;
using Server.Base.Core.Events.Arguments;
using Server.Base.Core.Extensions;
using Server.Base.Network.Services;
using System.Diagnostics;

namespace Server.Base.Core.Services;

public class CrashGuard(NetStateHandler handler, ILogger<CrashGuard> logger, EventSink sink,
    IServiceProvider services, InternalRConfig config) : IService
{
    private readonly Module[] _modules = services.GetServices<Module>().ToArray();

    public void Initialize() => sink.Crashed += OnCrash;

    public void OnCrash(CrashedEventArgs e)
    {
        GenerateCrashReport(e);

        Backup();

        Restart(e);
    }

    private void Restart(CrashedEventArgs e)
    {
        logger.LogDebug("Restarting...");

        try
        {
            Process.Start(GetExePath.Path());
            logger.LogInformation("Successfully restarted!");

            e.Close = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restart server");
        }
    }

    private void Backup()
    {
        logger.LogDebug("Backing up...");

        try
        {
            var timeStamp = GetTime.GetTimeStamp();

            var backup = Path.Combine(config.CrashBackupDirectory, timeStamp);

            InternalDirectory.CreateDirectory(backup);

            CopyFiles(config.SaveDirectory, config.CrashBackupDirectory);

            logger.LogInformation("Backed up!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to back up server.");
        }
    }

    private void CopyFiles(string originPath, string backupPath)
    {
        try
        {
            foreach (var fileLink in Directory.GetFiles(originPath))
            {
                var file = Path.GetFileName(fileLink);
                var oldF = Path.Combine(originPath, file);
                var newF = Path.Combine(backupPath, file);
                File.Copy(oldF, newF, true);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to copy file.");
        }
    }

    private void GenerateCrashReport(CrashedEventArgs crashedEventArgs)
    {
        logger.LogDebug("Generating report...");

        try
        {
            var timeStamp = GetTime.GetTimeStamp();
            var fileName = $"Crash {timeStamp}.log";

            var filePath = Path.Combine(config.CrashDirectory, fileName);

            using (var streamWriter = new StreamWriter(filePath))
            {
                streamWriter.WriteLine("Server Crash Report");
                streamWriter.WriteLine("===================");
                streamWriter.WriteLine();

                foreach (var module in _modules)
                    streamWriter.WriteLine(module.GetModuleInformation());
                streamWriter.WriteLine("Operating System: {0}", Environment.OSVersion);
                streamWriter.WriteLine(".NET Framework: {0}", Environment.Version);
                streamWriter.WriteLine("Time: {0}", DateTime.UtcNow);

                streamWriter.WriteLine();
                streamWriter.WriteLine("Exception:");
                streamWriter.WriteLine(crashedEventArgs.Exception);
                streamWriter.WriteLine();

                streamWriter.WriteLine("Clients:");

                try
                {
                    var netStates = handler.Instances;

                    streamWriter.WriteLine("- Count: {0}", netStates.Count);

                    foreach (var netState in netStates)
                    {
                        streamWriter.Write("+ {0}:", netState);

                        var account = netState.Get<Account>();

                        if (account != null)
                            streamWriter.Write(" (Account = {0})", account.Username);

                        streamWriter.WriteLine();
                    }
                }
                catch
                {
                    streamWriter.WriteLine("- Failed");
                }
            }

            logger.LogInformation("Logged error!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to log error.");
        }
    }
}
