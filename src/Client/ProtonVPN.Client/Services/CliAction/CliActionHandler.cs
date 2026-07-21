using ProtonVPN.Client.Logic.Connection.Contracts;
using ProtonVPN.Client.Logic.Connection.Contracts.Enums;
using ProtonVPN.Client.Logic.Connection.Contracts.Models.Intents;
using ProtonVPN.Client.Logic.Connection.Contracts.Models.Intents.Locations.Countries;
using ProtonVPN.Client.Logic.Services.Contracts;
using ProtonVPN.Common.Core.Extensions;
using ProtonVPN.Logging.Contracts;
using ProtonVPN.Logging.Contracts.Events.AppLogs;
using ProtonVPN.StatisticalEvents.Contracts.Dimensions;

namespace ProtonVPN.Client.Services.CliAction;

public class CliActionHandler
{
    private const string CLI_ARGS_FILE = "ProtonVPN\\cli_args.txt";

    private readonly IConnectionManager _connectionManager;
    private readonly IVpnServiceCaller _vpnServiceCaller;
    private readonly ILogger _logger;

    public CliActionHandler(
        IConnectionManager connectionManager,
        IVpnServiceCaller vpnServiceCaller,
        ILogger logger)
    {
        _connectionManager = connectionManager;
        _vpnServiceCaller = vpnServiceCaller;
        _logger = logger;
    }

    public CliAction DetectAction(string[] args)
    {
        bool isWait = false;
        CliActionType type = CliActionType.None;
        string? argument = null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (arg.EqualsIgnoringCase(CliArgTokens.Wait))
            {
                isWait = true;
            }
            else if (arg.EqualsIgnoringCase(CliArgTokens.Connect))
            {
                type = CliActionType.Connect;
                string? next = i + 1 < args.Length ? args[i + 1] : null;
                if (next != null && !next.StartsWith('-'))
                {
                    argument = next;
                    i++;
                }
            }
            else if (arg.EqualsIgnoringCase(CliArgTokens.Disconnect))
            {
                type = CliActionType.Disconnect;
            }
            else if (arg.EqualsIgnoringCase(CliArgTokens.Status))
            {
                type = CliActionType.Status;
            }
        }

        return new CliAction(type, argument, isWait);
    }

    public async Task<bool> ExecuteActionAsync(CliAction action)
    {
        try
        {
            switch (action.Type)
            {
                case CliActionType.Connect:
                    return await ConnectAsync(action);
                case CliActionType.Disconnect:
                    return await DisconnectAsync(action);
                case CliActionType.Status:
                    return PrintStatus();
                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error<AppLog>("CLI action failed.", ex);
            Console.Error.WriteLine($"Error: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> ConnectAsync(CliAction action)
    {
        IConnectionIntent? intent = ParseLocation(action.Argument);

        if (intent == null)
        {
            string msg = $"Unknown location '{action.Argument}'. Use a 2-letter country code, 'fastest', or 'random'.";
            _logger.Warn<AppLog>(msg);
            Console.Error.WriteLine(msg);
            return false;
        }

        _logger.Info<AppLog>($"CLI connect with intent: {intent}");
        await _connectionManager.ConnectAsync(VpnTriggerDimension.Auto, intent);

        if (!action.Wait)
        {
            Console.Out.WriteLine($"Connecting...");
            return true;
        }

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        while (!cts.Token.IsCancellationRequested)
        {
            await Task.Delay(500, cts.Token);
            if (_connectionManager.IsConnected)
            {
                Console.Out.WriteLine("Connected.");
                return true;
            }
            if (_connectionManager.HasError || _connectionManager.IsDisconnected)
            {
                Console.Error.WriteLine("Connection failed.");
                return false;
            }
            await _vpnServiceCaller.RepeatStateAsync();
        }

        Console.Error.WriteLine("Connection timed out.");
        return false;
    }

    private async Task<bool> DisconnectAsync(CliAction action)
    {
        _logger.Info<AppLog>("CLI disconnect");
        await _connectionManager.DisconnectAsync(VpnTriggerDimension.Auto);

        if (!action.Wait)
        {
            Console.Out.WriteLine("Disconnecting...");
            return true;
        }

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));
        while (!cts.Token.IsCancellationRequested)
        {
            await Task.Delay(500, cts.Token);
            if (_connectionManager.IsDisconnected)
            {
                Console.Out.WriteLine("Disconnected.");
                return true;
            }
            await _vpnServiceCaller.RepeatStateAsync();
        }

        Console.Error.WriteLine("Disconnect timed out.");
        return false;
    }

    private bool PrintStatus()
    {
        ConnectionStatus status = _connectionManager.ConnectionStatus;
        switch (status)
        {
            case ConnectionStatus.Disconnected:
                Console.Out.WriteLine("Disconnected");
                break;
            case ConnectionStatus.Connecting:
                Console.Out.WriteLine("Connecting...");
                break;
            case ConnectionStatus.Connected:
                var details = _connectionManager.CurrentConnectionDetails;
                if (details != null)
                {
                    Console.Out.WriteLine($"Connected to {details.ServerName} ({details.PhysicalServer?.EntryIp}, {details.Protocol})");
                }
                else
                {
                    Console.Out.WriteLine("Connected");
                }
                break;
        }
        return true;
    }

    private IConnectionIntent? ParseLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location) ||
            location.EqualsIgnoringCase("fastest"))
        {
            return ConnectionIntent.Default;
        }

        if (location.EqualsIgnoringCase("random"))
        {
            return new ConnectionIntent(MultiCountryLocationIntent.Random);
        }

        if (location.Length == 2)
        {
            return new ConnectionIntent(SingleCountryLocationIntent.From(location.ToUpperInvariant()));
        }

        return null;
    }

    public static void WriteArgsToTempFile(string[] args)
    {
        try
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ProtonVPN");
            Directory.CreateDirectory(tempDir);
            string filePath = Path.Combine(tempDir, "cli_args.txt");
            string content = string.Join("|", args);
            File.WriteAllText(filePath, content);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to write CLI args temp file: {ex.Message}");
        }
    }

    public static string[]? ReadAndDeleteArgsTempFile()
    {
        try
        {
            string filePath = Path.Combine(Path.GetTempPath(), CLI_ARGS_FILE);
            if (!File.Exists(filePath))
            {
                return null;
            }

            string content = File.ReadAllText(filePath);
            File.Delete(filePath);

            return content.Split('|', StringSplitOptions.RemoveEmptyEntries);
        }
        catch
        {
            return null;
        }
    }
}

public static class CliArgTokens
{
    public const string Connect = "--connect";
    public const string Disconnect = "--disconnect";
    public const string Status = "--status";
    public const string Wait = "--wait";

    public static bool ContainsCliAction(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].EqualsIgnoringCase(Connect) ||
                args[i].EqualsIgnoringCase(Disconnect) ||
                args[i].EqualsIgnoringCase(Status))
            {
                return true;
            }
        }
        return false;
    }
}
