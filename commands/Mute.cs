using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using Dapper;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

    // Estrutura de dados para Dapper
    public class MuteEntry
    {
        public ulong steamid { get; set; }
        public string? reason { get; set; }
        public DateTime timestamp { get; set; }
        public bool unmuted { get; set; }
    }

    // Carregamento de Mutes (Seguro, pois é chamado no Task.Run)
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

    // Método auxiliar para encontrar jogadores (Mantido aqui, pois lida apenas com entidades do jogo)
    private CCSPlayerController? FindPlayerByNameOrSteamId(CCSPlayerController? caller, string identifier)
    {
        if (ulong.TryParse(identifier, out var steamId))
        {
            var player = Utilities.GetPlayers().FirstOrDefault(p => p.AuthorizedSteamID?.SteamId64 == steamId);
            if (player != null) return player;
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
        }
        else
        {
            caller?.PrintToChat(_plugin.T("player_not_found_with_name", identifier));
        }
        return null;
    }


    // --- COMANDOS DELEGAÇÃO ---

    [RequiresPermissions("@css/chat")]
    public void MutePlayerCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2)
        {
            caller?.PrintToChat(_plugin.T("mute_usage"));
            return;
        }

        var targetIdentifier = info.GetArg(1);
        var reason = info.ArgCount > 2 ? string.Join(" ", Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i))) : _plugin.T("no_reason_provided_console");

        // Tenta encontrar o alvo pelo nome/SteamID. Se não encontrar, tenta SteamID do argumento.
        var targetPlayer = FindPlayerByNameOrSteamId(caller, targetIdentifier);

        ulong steamId = 0;

        if (targetPlayer != null && targetPlayer.AuthorizedSteamID != null)
        {
            steamId = targetPlayer.AuthorizedSteamID.SteamId64;
        }
        else if (ulong.TryParse(targetIdentifier, out var parsedSteamId))
        {
            steamId = parsedSteamId;
        }

        if (steamId != 0)
        {
            // DELEGA PARA O MANIPULADOR PROTEGIDO NA CLASSE PRINCIPAL
            _plugin.HandleMute(caller, steamId, reason, targetPlayer?.PlayerName);
        }
        else
        {
            caller?.PrintToChat(_plugin.T("player_not_found"));
        }
    }

    // MÉTODOS HANDLE REMOVIDOS DESTA CLASSE! (Movidos para AdminControlPlugin.cs)
    // public async void HandleMute(...) {...}

    [RequiresPermissions("@css/chat")]
    public void UnmutePlayerCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var steamId))
        {
            caller?.PrintToChat(_plugin.T("unmute_usage"));
            return;
        }

        // DELEGA PARA O MANIPULADOR PROTEGIDO NA CLASSE PRINCIPAL
        _plugin.HandleUnmute(caller, steamId);
    }

    // MÉTODOS HANDLE REMOVIDOS DESTA CLASSE! (Movidos para AdminControlPlugin.cs)
    // public async void HandleUnmute(...) {...}
}