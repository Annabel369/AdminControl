using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Dapper;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdminControlPlugin.commands;

public class Admin
{
    private readonly AdminControlPlugin _plugin;

    public Admin(AdminControlPlugin plugin)
    {
        _plugin = plugin;
    }

    public class AdminEntry
    {
        [JsonPropertyName("identity")]
        public string? Identity { get; set; }
        [JsonPropertyName("immunity")]
        public int Immunity { get; set; }
        [JsonPropertyName("flags")]
        public List<string> Flags { get; set; } = new List<string>();
        [JsonPropertyName("groups")]
        public List<string> Groups { get; set; } = new List<string>();
    }

    public class AdminFile
    {
        public Dictionary<string, AdminEntry> Admins { get; set; } = new Dictionary<string, AdminEntry>();
    }

    public class DbAdmin
    {
        public ulong steamid { get; set; }
        public string? name { get; set; }
        public string? permission { get; set; }
        public int level { get; set; }
    }

    public async Task GenerateAdminsJsonAsync()
    {
        try
        {
            await RemoveExpiredAdminsAsync();
            using var connection = await _plugin.GetOpenConnectionAsync();
            var admins = await connection.QueryAsync<DbAdmin>("SELECT steamid, name, permission, level FROM admins;");
            var adminFile = new AdminFile();

            foreach (var admin in admins)
            {
                adminFile.Admins[admin.name!] = new AdminEntry
                {
                    Identity = admin.steamid.ToString(),
                    Immunity = admin.level,
                    Flags = new List<string> { admin.permission! }
                };
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var jsonString = JsonSerializer.Serialize(adminFile.Admins, options);

            var path = Path.Combine(_plugin.ModuleDirectory, "../../configs/admins.json");
            await File.WriteAllTextAsync(path, jsonString);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminControlPlugin] {_plugin.T("error_generating_admins_json", ex.Message)}");
        }
    }

    public async Task RemoveExpiredAdminsAsync()
    {
        try
        {
            using var connection = await _plugin.GetOpenConnectionAsync();
            var expiredAdmins = await connection.ExecuteAsync("DELETE FROM admins WHERE expires_at IS NOT NULL AND expires_at < NOW();");

            if (expiredAdmins > 0)
            {
                Console.WriteLine($"[AdminControlPlugin] {_plugin.T("log_expired_admins_removed", expiredAdmins)}");
                await GenerateAdminsJsonAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminControlPlugin] {_plugin.T("error_removing_expired_admins", ex.Message)}");
        }
    }

    [RequiresPermissions("@css/root")]
    public void GrantCustomAdmin(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 6 ||
          !ulong.TryParse(info.GetArg(1), out var steamId) ||
          !int.TryParse(info.GetArg(4), out var level) ||
          !int.TryParse(info.GetArg(5), out var durationMinutes))
        {
            caller?.PrintToChat(_plugin.T("add_admin_usage"));
            Server.PrintToConsole(_plugin.T("add_admin_usage"));
            return;
        }
        var name = info.GetArg(2);
        var permission = info.GetArg(3);
        var expiresAt = DateTime.UtcNow.AddMinutes(durationMinutes);
        Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_add_admin_attempt", caller?.PlayerName ?? "Console")}");
        HandleGrantAdmin(caller, steamId, name, permission, level, expiresAt);
    }

    public async void HandleGrantAdmin(CCSPlayerController? caller, ulong steamId, string name, string permission, int level, DateTime? expiresAt)
    {
        try
        {
            using var connection = await _plugin.GetOpenConnectionAsync();
            await connection.ExecuteAsync(@"
                INSERT INTO admins (steamid, name, permission, level, expires_at, granted_by)
                VALUES (@SteamId, @Name, @Permission, @Level, @ExpiresAt, @GrantedBy)
                ON DUPLICATE KEY UPDATE
                    name = @Name,
                    permission = @Permission,
                    level = @Level,
                    expires_at = @ExpiresAt,
                    granted_by = @GrantedBy,
                    timestamp = CURRENT_TIMESTAMP;",
              new
              {
                  SteamId = steamId,
                  Name = name,
                  Permission = permission,
                  Level = level,
                  ExpiresAt = expiresAt,
                  GrantedBy = caller?.AuthorizedSteamID?.SteamId64
              });
            Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_admin_added", caller?.PlayerName ?? "Console", name, permission, level)}");

            await GenerateAdminsJsonAsync();
            caller?.PrintToChat(_plugin.T("admin_added", name, permission));
        }
        catch (Exception ex)
        {
            caller?.PrintToChat(_plugin.T("error_adding_admin"));
            Console.WriteLine($"[AdminControlPlugin] {_plugin.T("error_adding_admin_log", ex.Message)}");
        }
    }

    [RequiresPermissions("@css/root")]
    public void RemoveAdminCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var steamId))
        {
            caller?.PrintToChat(_plugin.T("remove_admin_usage"));
            Server.PrintToConsole(_plugin.T("remove_admin_usage"));
            return;
        }
        HandleRemoveAdmin(caller, steamId);
    }

    public async void HandleRemoveAdmin(CCSPlayerController? caller, ulong steamId)
    {
        try
        {
            await using var connection = await _plugin.GetOpenConnectionAsync();
            await connection.ExecuteAsync("DELETE FROM admins WHERE steamid = @SteamId;", new { SteamId = steamId });
            Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_admin_removed", caller?.PlayerName ?? "Console", steamId)}"); await GenerateAdminsJsonAsync();
            caller?.PrintToChat(_plugin.T("admin_removed"));
        }
        catch (Exception ex)
        {
            caller?.PrintToChat(_plugin.T("error_removing_admin"));
            Console.WriteLine($"[AdminControlPlugin] {_plugin.T("error_removing_admin_log", ex.Message)}");
        }
    }

    [RequiresPermissions("@css/root")]
    public void ReloadAdminsCommand(CCSPlayerController? caller, CommandInfo info)
    {
        Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_reload_admins_attempt", caller?.PlayerName ?? "Console")}");
        Task.Run(async () =>
        {
            await GenerateAdminsJsonAsync();
            caller?.PrintToChat(_plugin.T("admins_reloaded_success"));
            Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_admins_reloaded_success")}");
        });
    }

    [RequiresPermissions("@css/kick")]
    public void KickCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2)
        {
            caller?.PrintToChat(_plugin.T("kick_usage"));
            return;
        }

        var targetName = info.GetArg(1);
        var target = FindPlayerByName(caller, targetName);

        if (target != null)
        {
            Server.ExecuteCommand($"kick \"{target.PlayerName}\"");
            caller?.PrintToChat(_plugin.T("player_kicked", target.PlayerName));
            Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_player_kicked", caller?.PlayerName ?? "Console", target.PlayerName)}");
        }
        else
        {
            caller?.PrintToChat(_plugin.T("player_not_found"));
        }
    }

    [RequiresPermissions("@css/slay")]
    public void SwapTeamCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2)
        {
            caller?.PrintToChat(_plugin.T("swap_team_usage"));
            return;
        }

        var targetName = info.GetArg(1);
        var target = FindPlayerByName(caller, targetName);

        if (target != null)
        {
            var newTeam = target.Team == CsTeam.Terrorist ? CsTeam.CounterTerrorist : CsTeam.Terrorist;
            target.SwitchTeam(newTeam);
            caller?.PrintToChat(_plugin.T("player_swapped", target.PlayerName, newTeam));
            Server.PrintToConsole($"[AdminControlPlugin] {_plugin.T("log_player_swapped", caller?.PlayerName ?? "Console", target.PlayerName, newTeam)}");
        }
        else
        {
            caller?.PrintToChat(_plugin.T("player_not_found"));
        }
    }

    private CCSPlayerController? FindPlayerByName(CCSPlayerController? caller, string name)
    {
        var players = Utilities.GetPlayers()
          .Where(p => p.IsValid && p.PlayerName.Contains(name, StringComparison.OrdinalIgnoreCase))
          .ToList();

        if (players.Count == 1)
        {
            return players.First();
        }

        if (players.Count > 1)
        {
            caller?.PrintToChat(_plugin.T("multiple_players_found"));
            foreach (var p in players)
            {
                caller?.PrintToChat($"  - {p.PlayerName} (SteamID: {p.AuthorizedSteamID?.SteamId64})");
            }
        }
        else
        {
            caller?.PrintToChat(_plugin.T("player_not_found_with_name", name));
        }

        return null;
    }
}