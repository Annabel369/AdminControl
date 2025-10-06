using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using Dapper;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AdminControlPlugin.commands;

public class Ban
{
    private readonly AdminControlPlugin _plugin;
    private readonly HashSet<ulong> _bannedPlayers = new HashSet<ulong>();
    private readonly HashSet<string> _bannedIps = new HashSet<string>();
    private readonly Dictionary<ulong, string> _banReasons = new Dictionary<ulong, string>();
    private readonly Dictionary<string, string> _ipBanReasons = new Dictionary<string, string>();

    public Ban(AdminControlPlugin plugin)
    {
        _plugin = plugin;
    }

    public bool IsBanned(ulong steamId) => _bannedPlayers.Contains(steamId);
    public string? GetBanReason(ulong steamId) => _banReasons.TryGetValue(steamId, out var reason) ? reason : null;
    public bool IsIpBanned(string ip) => _bannedIps.Contains(ip);
    public string? GetIpBanReason(string ip) => _ipBanReasons.TryGetValue(ip, out var reason) ? reason : null;

    // Classes de entrada para o banco de dados (MANTIDAS, necessárias para Dapper)
    public class BanEntry
    {
        public ulong steamid { get; set; }
        public string? reason { get; set; }
        public DateTime timestamp { get; set; }
    }

    public class IpBanEntry
    {
        public bool unbanned { get; set; } = false;
        public string ip_address { get; set; } = string.Empty;
        public string reason { get; set; } = string.Empty;
        public DateTime timestamp { get; set; }
    }

    // Método para carregar os bans (Chamado pelo AdminControlPlugin.cs)
    public async Task LoadBansFromDatabaseAsync()
    {
        try
        {
            using var connection = await _plugin.GetOpenConnectionAsync();
            var steamBans = await connection.QueryAsync<BanEntry>("SELECT steamid, reason FROM bans WHERE unbanned = FALSE;");
            var ipBans = await connection.QueryAsync<IpBanEntry>("SELECT ip_address, reason FROM ip_bans WHERE unbanned = FALSE;");

            _bannedPlayers.Clear();
            _bannedIps.Clear();
            _banReasons.Clear();
            _ipBanReasons.Clear();

            foreach (var ban in steamBans)
            {
                _bannedPlayers.Add(ban.steamid);
                _banReasons[ban.steamid] = ban.reason ?? _plugin.T("no_reason");
            }

            foreach (var ban in ipBans)
            {
                if (!string.IsNullOrWhiteSpace(ban.ip_address))
                {
                    var cleanIp = ban.ip_address.Split(':')[0];
                    _bannedIps.Add(cleanIp);
                    _ipBanReasons[cleanIp] = ban.reason ?? _plugin.T("no_reason");
                }
            }

            Console.WriteLine($"[AdminControlPlugin] {_plugin.T("log_bans_loaded", steamBans.Count(), ipBans.Count())}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminControlPlugin] {_plugin.T("error_loading_bans", ex.Message)}");
        }
    }

    // --- COMANDOS DELEGAÇÃO ---

    [RequiresPermissions("@css/ban")]
    public void BanPlayerCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var steamId))
        {
            caller?.PrintToChat(_plugin.T("ban_player_usage"));
            return;
        }
        var reason = string.Join(" ", Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i)));

        // CHAMA O MANIPULADOR PROTEGIDO NA CLASSE PRINCIPAL
        _plugin.HandleBan(caller, steamId, reason);
    }

    [RequiresPermissions("@css/unban")]
    public void UnbanPlayerCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var steamId))
        {
            caller?.PrintToChat(_plugin.T("unban_player_usage"));
            return;
        }
        // CHAMA O MANIPULADOR PROTEGIDO NA CLASSE PRINCIPAL
        _plugin.HandleUnban(caller, steamId);
    }

    [RequiresPermissions("@css/unban")]
    public void UnbanIpCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2)
        {
            caller?.PrintToChat(_plugin.T("unban_ip_usage"));
            return;
        }
        var ipAddress = info.GetArg(1);

        // CHAMA O MANIPULADOR PROTEGIDO NA CLASSE PRINCIPAL
        _plugin.HandleUnbanIp(caller, ipAddress);
    }

    [RequiresPermissions("@css/rcon")]
    public async void ListBans(CCSPlayerController? caller, CommandInfo info)
    {
        // Este é um dos poucos métodos que pode ser assíncrono aqui, pois lida apenas com leitura
        // e usa Server.NextFrame para a saída, o que é seguro.
        try
        {
            using var connection = await _plugin.GetOpenConnectionAsync();
            var steamBans = await connection.QueryAsync<BanEntry>("SELECT steamid, reason, timestamp FROM bans WHERE unbanned = FALSE;");
            var ipBans = await connection.QueryAsync<IpBanEntry>("SELECT ip_address, reason, timestamp FROM ip_bans WHERE unbanned = FALSE;");

            Server.NextFrame(() =>
            {
                caller?.PrintToChat(_plugin.T("ban_list_header"));
                // ... (lógica de PrintToChat/PrintToConsole para steamBans e ipBans) ...
                foreach (var ban in steamBans)
                {
                    caller?.PrintToChat(_plugin.T("steamid_ban_entry", ban.steamid, ban.reason, ban.timestamp));
                }
                foreach (var ban in ipBans)
                {
                    caller?.PrintToChat(_plugin.T("ip_ban_entry", ban.ip_address, ban.reason, ban.timestamp));
                }
            });
        }
        catch (Exception ex)
        {
            Server.NextFrame(() => caller?.PrintToChat(_plugin.T("error_listing_bans", ex.Message)));
        }
    }

    internal void HandleBan(CCSPlayerController? cCSPlayerController, ulong targetSteamId, string v)
    {
        throw new NotImplementedException();
    }

    internal void IpBanPlayerCommand(CCSPlayerController p, CommandInfo commandInfo)
    {
        throw new NotImplementedException();
    }
}