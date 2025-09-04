using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using Dapper;
using System.Net;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;
using System.Collections.Generic;
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

    // Classe de entrada para o banco de dados
    public class BanEntry
    {
        public ulong steamid { get; set; }
        public string? reason { get; set; }
        public DateTime timestamp { get; set; }
    }

    public class IpBanEntry
    {
        internal bool unbanned;

        public string ip_address { get; set; } = string.Empty;
        public string reason { get; set; } = string.Empty;
        public DateTime timestamp { get; set; }
    }

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

            // Processa bans por IP, removendo a porta se houver
            foreach (var ban in ipBans)
            {
                if (!string.IsNullOrWhiteSpace(ban.ip_address))
                {
                    // Remove a porta (ex: "192.168.0.1:27015" → "192.168.0.1")
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

    [RequiresPermissions("@css/ban")]
    public void BanPlayerCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var steamId))
        {
            caller?.PrintToChat(_plugin.T("ban_player_usage"));
            Server.PrintToConsole(_plugin.T("ban_player_usage"));
            return;
        }
        Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_ban_attempt", caller?.PlayerName ?? "Console", steamId)}");
        var reason = string.Join(" ", Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i)));
        HandleBan(caller, steamId, reason);
    }

    public async void HandleBan(CCSPlayerController? caller, ulong steamId, string reason)
    {
        try
        {
            using var connection = await _plugin.GetOpenConnectionAsync();

            var isAlreadyBanned = await connection.ExecuteScalarAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM bans WHERE steamid = @SteamId AND unbanned = FALSE)", new { SteamId = steamId });

            if (isAlreadyBanned)
            {
                caller?.PrintToChat(_plugin.T("player_already_banned"));
                return;
            }

            await connection.ExecuteAsync(@"
                INSERT INTO bans (steamid, reason, unbanned) VALUES (@SteamId, @Reason, FALSE)
                ON DUPLICATE KEY UPDATE reason = @Reason, unbanned = FALSE, timestamp = CURRENT_TIMESTAMP;",
                new { SteamId = steamId, Reason = reason });

            _bannedPlayers.Add(steamId);
            _banReasons[steamId] = reason;

            Server.NextFrame(() =>
            {
                Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_player_banned_reason", caller?.PlayerName ?? "Console", steamId, reason)}");

                var playerToKick = Utilities.GetPlayers().FirstOrDefault(p => p.AuthorizedSteamID?.SteamId64 == steamId);
                if (playerToKick != null)
                {
                    Server.ExecuteCommand($"kickid {playerToKick.UserId} \"{_plugin.T("kick_ban_message", reason)}\"");
                }

                Server.ExecuteCommand($"banid 0 {steamId}");
                Server.ExecuteCommand($"writeid");

                caller?.PrintToChat(_plugin.T("player_banned", steamId, reason));
            });
        }
        catch (Exception ex)
        {
            caller?.PrintToChat(_plugin.T("error_banning_player"));
            Console.WriteLine($"[AdminControlPlugin] {_plugin.T("error_banning_player_log", ex.Message)}");
        }
    }

    [RequiresPermissions("@css/unban")]
    public void UnbanPlayerCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var steamId))
        {
            caller?.PrintToChat(_plugin.T("unban_player_usage"));
            Server.PrintToConsole(_plugin.T("unban_player_usage"));
            return;
        }
        Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_unban_attempt", caller?.PlayerName ?? "Console", steamId)}");

        // Chamada do método asíncrono e espera por ele.
        Task.Run(() => HandleUnban(caller, steamId));
    }

    // AQUI ESTÁ A MUDANÇA CRÍTICA:
    // O método agora retorna um Task, permitindo que o chamador espere pela sua conclusão.
    public async Task HandleUnban(CCSPlayerController? caller, ulong steamId)
    {
        try
        {
            using var connection = await _plugin.GetOpenConnectionAsync();
            var updatedRows = await connection.ExecuteAsync("UPDATE bans SET unbanned = TRUE WHERE steamid = @SteamId AND unbanned = FALSE;",
                new { SteamId = steamId });

            if (updatedRows > 0)
            {
                _bannedPlayers.Remove(steamId);
                _banReasons.Remove(steamId);

                Server.NextFrame(() =>
                {
                    Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_player_unbanned", caller?.PlayerName ?? "Console", steamId)}");
                    Server.ExecuteCommand($"removeid {steamId}");
                    Server.ExecuteCommand($"writeid");
                    caller?.PrintToChat(_plugin.T("player_unbanned", steamId));
                });
            }
            else
            {
                // Mensagem de erro se o jogador não foi encontrado ou já estava desbanido
                caller?.PrintToChat(_plugin.T("player_not_banned", steamId));
            }
        }
        catch (Exception ex)
        {
            caller?.PrintToChat(_plugin.T("error_unbanning_player"));
            Console.WriteLine($"[AdminControlPlugin] {_plugin.T("error_unbanning_player_log", ex.Message)}");
        }
    }

    [RequiresPermissions("@css/ban")]
    public void IpBanPlayerCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2)
        {
            caller?.PrintToChat(_plugin.T("ip_ban_usage"));
            Server.PrintToConsole(_plugin.T("ip_ban_usage"));
            return;
        }

        var rawIp = info.GetArg(1);
        var ipAddress = rawIp.Split(':')[0];

        if (!IPAddress.TryParse(ipAddress, out _))
        {
            caller?.PrintToChat(_plugin.T("ip_ban_usage"));
            Server.PrintToConsole(_plugin.T("ip_ban_usage"));
            return;
        }

        var reason = info.ArgCount > 2
            ? string.Join(" ", Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i)))
            : _plugin.T("no_reason_specified");

        Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_ip_ban_attempt", caller?.PlayerName ?? "Console", ipAddress, reason)}");
        HandleIpBan(caller, ipAddress, reason);
    }

    public async void HandleIpBan(CCSPlayerController? caller, string ipAddress, string reason)
    {
        try
        {
            // Remove a porta, se houver
            ipAddress = ipAddress.Split(':')[0];

            using var connection = await _plugin.GetOpenConnectionAsync();

            // Verifica se o IP já existe no banco
            var existingBan = await connection.QueryFirstOrDefaultAsync<IpBanEntry>(
                "SELECT * FROM ip_bans WHERE ip_address = @IpAddress LIMIT 1",
                new { IpAddress = ipAddress });

            if (existingBan != null)
            {
                if (!existingBan.unbanned)
                {
                    // Já está banido
                    caller?.PrintToChat(_plugin.T("ip_already_banned", ipAddress));
                    return;
                }
                else
                {
                    // Está desbanido — atualiza para banido novamente
                    await connection.ExecuteAsync(@"
                    UPDATE ip_bans
                    SET reason = @Reason, timestamp = NOW(), unbanned = FALSE
                    WHERE ip_address = @IpAddress;",
                        new { IpAddress = ipAddress, Reason = reason });

                    Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_ip_rebanned", caller?.PlayerName ?? "Console", ipAddress)}");
                }
            }
            else
            {
                // Não existe — insere novo banimento
                await connection.ExecuteAsync(@"
                INSERT INTO ip_bans (ip_address, reason, timestamp, unbanned)
                VALUES (@IpAddress, @Reason, NOW(), FALSE);",
                    new { IpAddress = ipAddress, Reason = reason });

                Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_ip_banned", caller?.PlayerName ?? "Console", ipAddress)}");
            }

            // Atualiza listas internas
            _bannedIps.Add(ipAddress);
            _ipBanReasons[ipAddress] = reason;

            // Executa comandos no servidor
            Server.NextFrame(() =>
            {
                Server.ExecuteCommand($"kickip {ipAddress} \"{_plugin.T("kick_ip_ban_message", reason)}\"");
                Server.ExecuteCommand($"banip 0 {ipAddress}");
                Server.ExecuteCommand($"writeip");
                caller?.PrintToChat(_plugin.T("ip_banned", ipAddress, reason));
            });
        }
        catch (Exception ex)
        {
            caller?.PrintToChat(_plugin.T("error_banning_ip"));
            Console.WriteLine($"[AdminControlPlugin] {_plugin.T("error_banning_ip_log", ex.Message)}");
        }
    }

    [RequiresPermissions("@css/unban")]
    public void UnbanIpPlayerCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2)
        {
            caller?.PrintToChat(_plugin.T("unban_ip_usage"));
            Server.PrintToConsole(_plugin.T("unban_ip_usage"));
            return;
        }
        var ipAddress = info.GetArg(1);
        Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_unban_ip_attempt", caller?.PlayerName ?? "Console", ipAddress)}");

        // Chamada do método asíncrono e espera por ele.
        Task.Run(() => HandleUnbanIp(caller, ipAddress));
    }

    // AQUI ESTÁ A MUDANÇA CRÍTICA:
    // O método agora retorna um Task, permitindo que o chamador espere pela sua conclusão.
    public async Task HandleUnbanIp(CCSPlayerController? caller, string ipAddress)
    {
        try
        {
            using var connection = await _plugin.GetOpenConnectionAsync();

            var updatedRows = await connection.ExecuteAsync(@"
                UPDATE ip_bans
                SET unbanned = TRUE
                WHERE ip_address = @IpAddress AND unbanned = FALSE;",
                new { IpAddress = ipAddress });

            if (updatedRows > 0)
            {
                if (_bannedIps.Remove(ipAddress))
                {
                    _ipBanReasons.Remove(ipAddress);
                }
                Server.NextFrame(() =>
                {
                    Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_unbanned_ip", caller?.PlayerName ?? "Console", ipAddress)}");
                    Server.ExecuteCommand($"removeip {ipAddress}");
                    Server.ExecuteCommand($"writeip");
                    caller?.PrintToChat(_plugin.T("ip_unbanned", ipAddress));
                });
            }
            else
            {
                // Mensagem de erro se o IP não foi encontrado ou já estava desbanido
                caller?.PrintToChat(_plugin.T("ip_not_banned", ipAddress));
            }
        }
        catch (Exception ex)
        {
            caller?.PrintToChat(_plugin.T("error_unbanning_ip"));
            Console.WriteLine($"[AdminControlPlugin] {_plugin.T("error_unbanning_ip_log", ex.Message)}");
        }
    }

    [RequiresPermissions("@css/rcon")]
    public async void ListBans(CCSPlayerController? caller, CommandInfo info)
    {
        try
        {
            Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_list_bans_attempt", caller?.PlayerName ?? "Console")}");
            using var connection = await _plugin.GetOpenConnectionAsync();
            var steamBans = await connection.QueryAsync<BanEntry>("SELECT steamid, reason, timestamp FROM bans WHERE unbanned = FALSE;");
            var ipBans = await connection.QueryAsync<IpBanEntry>("SELECT ip_address, reason, timestamp FROM ip_bans WHERE unbanned = FALSE;");

            Server.NextFrame(() =>
            {
                caller?.PrintToChat(_plugin.T("ban_list_header"));
                caller?.PrintToChat(_plugin.T("steamid_bans_header"));
                Server.PrintToConsole(_plugin.T("ban_list_header"));
                Server.PrintToConsole(_plugin.T("steamid_bans_header"));
                foreach (var ban in steamBans)
                {
                    caller?.PrintToChat(_plugin.T("steamid_ban_entry", ban.steamid, ban.reason, ban.timestamp));
                    Server.PrintToConsole(_plugin.T("steamid_ban_entry", ban.steamid, ban.reason, ban.timestamp));
                }
                caller?.PrintToChat(_plugin.T("ip_bans_header"));
                Server.PrintToConsole(_plugin.T("ip_bans_header"));
                foreach (var ban in ipBans)
                {
                    caller?.PrintToChat(_plugin.T("ip_ban_entry", ban.ip_address, ban.reason, ban.timestamp));
                    Server.PrintToConsole(_plugin.T("ip_ban_entry", ban.ip_address, ban.reason, ban.timestamp));
                }
                caller?.PrintToChat(_plugin.T("ban_list_footer"));
                Server.PrintToConsole(_plugin.T("ban_list_footer"));
            });
        }
        catch (Exception ex)
        {
            caller?.PrintToChat(_plugin.T("error_listing_bans", ex.Message));
            Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("error_listing_bans_log", ex.Message)}");
        }
    }
}