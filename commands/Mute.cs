using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Dapper;

namespace AdminControlPlugin.commands;

public class Mute
{
    private readonly AdminControlPlugin _plugin;
    private readonly HashSet<ulong> _mutedPlayers = new HashSet<ulong>();

    public Mute(AdminControlPlugin plugin)
    {
        _plugin = plugin;
    }

    public bool IsMuted(ulong steamId) => _mutedPlayers.Contains(steamId);

    public class MuteEntry
    {
        public ulong steamid { get; set; }
        public string? reason { get; set; }
        public DateTime timestamp { get; set; }
        public bool unmuted { get; set; }
    }

    public async Task LoadMutesFromDatabaseAsync()
    {
        try
        {
            using var connection = await _plugin.GetOpenConnectionAsync();
            var mutedPlayers = await connection.QueryAsync<MuteEntry>("SELECT steamid FROM mutes WHERE unmuted = FALSE;");

            _mutedPlayers.Clear();
            foreach (var mute in mutedPlayers)
            {
                _mutedPlayers.Add(mute.steamid);
            }
            Console.WriteLine($"[AdminControlPlugin] {_plugin.T("log_mutes_loaded", mutedPlayers.Count())}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminControlPlugin] {_plugin.T("error_loading_mutes", ex.Message)}");
        }
    }

    [RequiresPermissions("@css/chat")]
    public void MutePlayerCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2)
        {
            caller?.PrintToChat(_plugin.T("mute_usage"));
            Server.PrintToConsole(_plugin.T("mute_usage"));
            return;
        }

        var targetIdentifier = info.GetArg(1);
        var target = FindPlayerByNameOrSteamId(caller, targetIdentifier);

        if (target != null)
        {
            var reason = info.ArgCount > 2 ? string.Join(" ", Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i))) : _plugin.T("no_reason_provided_console");

            target.VoiceFlags = (VoiceFlags)0;
            caller?.PrintToChat(_plugin.T("player_muted", target.PlayerName));
            Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_player_muted_reason", caller?.PlayerName ?? "Console", target.PlayerName, target.AuthorizedSteamID!.SteamId64, reason)}");

            HandleMute(caller, target.AuthorizedSteamID!.SteamId64, reason);
        }
        else
        {
            caller?.PrintToChat(_plugin.T("player_not_found"));
            Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_mute_failed", targetIdentifier)}");
        }
    }

    public async void HandleMute(CCSPlayerController? caller, ulong steamId, string reason)
    {
        try
        {
            using var connection = await _plugin.GetOpenConnectionAsync();
            await connection.ExecuteAsync(@"
                INSERT INTO mutes (steamid, reason) VALUES (@SteamId, @Reason)
                ON DUPLICATE KEY UPDATE unmuted = FALSE, timestamp = CURRENT_TIMESTAMP;",
              new { SteamId = steamId, Reason = reason });

            _mutedPlayers.Add(steamId);
            caller?.PrintToChat(_plugin.T("mute_success"));
        }
        catch (Exception ex)
        {
            caller?.PrintToChat(_plugin.T("mute_error"));
            Console.WriteLine($"[AdminControlPlugin] {_plugin.T("mute_error_log", ex.Message)}");
        }
    }

    [RequiresPermissions("@css/chat")]
    public void UnmutePlayerCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var steamId))
        {
            caller?.PrintToChat(_plugin.T("unmute_usage"));
            Server.PrintToConsole(_plugin.T("unmute_usage"));
            return;
        }
        Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_unmute_attempt", caller?.PlayerName ?? "Console", steamId)}");
        HandleUnmute(caller, steamId);
    }

    public async void HandleUnmute(CCSPlayerController? caller, ulong steamId)
    {
        try
        {
            using var connection = await _plugin.GetOpenConnectionAsync();
            var rowsAffected = await connection.ExecuteAsync(@"
                UPDATE mutes
                SET unmuted = TRUE
                WHERE steamid = @SteamId AND unmuted = FALSE;",
              new { SteamId = steamId });

            _mutedPlayers.Remove(steamId);

            var targetPlayer = Utilities.GetPlayers().FirstOrDefault(p => p.AuthorizedSteamID?.SteamId64 == steamId);
            if (targetPlayer != null)
            {
                targetPlayer.VoiceFlags = (VoiceFlags)2;
                caller?.PrintToChat(_plugin.T("player_unmuted_name", targetPlayer.PlayerName));
            }
            else
            {
                caller?.PrintToChat(_plugin.T("player_unmuted_steamid", steamId));
            }
            Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_player_unmuted_steamid_log", caller?.PlayerName ?? "Console", steamId)}");
        }
        catch (Exception ex)
        {
            caller?.PrintToChat(_plugin.T("unmute_error"));
            Console.WriteLine($"[AdminControlPlugin] {_plugin.T("unmute_error_log", ex.Message)}");
        }
    }

    private CCSPlayerController? FindPlayerByNameOrSteamId(CCSPlayerController? caller, string identifier)
    {
        if (ulong.TryParse(identifier, out var steamId))
        {
            var player = Utilities.GetPlayers().FirstOrDefault(p => p.AuthorizedSteamID?.SteamId64 == steamId);
            if (player != null)
            {
                return player;
            }
        }

        var players = Utilities.GetPlayers()
          .Where(p => p.IsValid && p.PlayerName.Contains(identifier, StringComparison.OrdinalIgnoreCase))
          .ToList();

        if (players.Count == 1)
        {
            return players.First();
        }

        if (players.Count > 1)
        {
            caller?.PrintToChat(_plugin.T("multiple_players_found"));
            Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_multiple_players_found", identifier)}");
        }
        else
        {
            caller?.PrintToChat(_plugin.T("player_not_found_with_name", identifier));
            Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_player_not_found", identifier)}");
        }
        return null;
    }
}